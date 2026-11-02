import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron2 } from '../internal/Chevron2.jsx';
import { Placeholder4 } from '../internal/Placeholder4.jsx';
import { SecondaryAction } from '../internal/SecondaryAction.jsx';

// figma node: 9026:1317 Split button (150 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size) + '|' + "layout=" + __venc(p.layout);

export function SplitButton(_p = {}) {
  _p = __slateNorm(_p, {"style2":["primary","secondary (default)","outline","subtle","transparent"],"state":["rest","hover","pressed","selected","disabled"],"size":["lg","medium (default)","sm"],"layout":["icon and label (default)","icon only"]});
  const props = { ..._p, label: _p.label ?? "Text", focus: _p.focus ?? false, style2: _p.style2 ?? "primary", state: _p.state ?? "rest", size: _p.size ?? "lg", layout: _p.layout ?? "icon and label (default)", icon: _p.icon ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"disabled"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"disabled"} />}</div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-selected)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-selected)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-brand-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-brand-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-hover)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-brand-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 38,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={38} height={40} viewBox="0 0 38 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 38,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 38 0 L 39 0 L 39 -1 L 38 -1 L 38 0 Z M 38 40 L 38 41 L 39 41 L 39 40 L 38 40 Z M 4 0 L 4 1 L 38 1 L 38 0 L 38 -1 L 4 -1 L 4 0 Z M 38 0 L 37 0 L 37 40 L 38 40 L 39 40 L 39 0 L 38 0 Z M 38 40 L 38 39 L 4 39 L 4 40 L 4 41 L 38 41 L 38 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 90,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={90} height={40} viewBox="0 0 90 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 90,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 90 0 L 91 0 L 91 -1 L 90 -1 L 90 0 Z M 90 40 L 90 41 L 91 41 L 91 40 L 90 40 Z M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 90 0 L 89 0 L 89 40 L 90 40 L 91 40 L 91 0 L 90 0 Z M 90 40 L 90 39 L 4 39 L 4 40 L 4 41 L 90 41 L 90 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"disabled"} />}</div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"disabled"} />}</div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-selected)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-selected)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-brand-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-brand-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-hover)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-brand-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 38,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={38} height={40} viewBox="0 0 38 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 38,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 38 0 L 39 0 L 39 -1 L 38 -1 L 38 0 Z M 38 40 L 38 41 L 39 41 L 39 40 L 38 40 Z M 4 0 L 4 1 L 38 1 L 38 0 L 38 -1 L 4 -1 L 4 0 Z M 38 0 L 37 0 L 37 40 L 38 40 L 39 40 L 39 0 L 38 0 Z M 38 40 L 38 39 L 4 39 L 4 40 L 4 41 L 38 41 L 38 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 90,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={90} height={40} viewBox="0 0 90 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 90,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 90 0 L 91 0 L 91 -1 L 90 -1 L 90 0 Z M 90 40 L 90 41 L 91 41 L 91 40 L 90 40 Z M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 90 0 L 89 0 L 89 40 L 90 40 L 91 40 L 91 0 L 90 0 Z M 90 40 L 90 39 L 4 39 L 4 40 L 4 41 L 90 41 L 90 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"disabled"} />}</div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"disabled"} />}</div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "2px solid var(--neutral-stroke-1-selected)",
        borderRight: "2px solid var(--neutral-stroke-1-selected)",
        borderBottom: "2px solid var(--neutral-stroke-1-selected)",
        borderLeft: "2px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-selected)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "2px solid var(--neutral-stroke-1-selected)",
        borderRight: "2px solid var(--neutral-stroke-1-selected)",
        borderBottom: "2px solid var(--neutral-stroke-1-selected)",
        borderLeft: "2px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-1-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-pressed)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-hover)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-1-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 38,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={38} height={40} viewBox="0 0 38 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 38,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 38 0 L 39 0 L 39 -1 L 38 -1 L 38 0 Z M 38 40 L 38 41 L 39 41 L 39 40 L 38 40 Z M 4 0 L 4 1 L 38 1 L 38 0 L 38 -1 L 4 -1 L 4 0 Z M 38 0 L 37 0 L 37 40 L 38 40 L 39 40 L 39 0 L 38 0 Z M 38 40 L 38 39 L 4 39 L 4 40 L 4 41 L 38 41 L 38 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 90,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={90} height={40} viewBox="0 0 90 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 90,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 90 0 L 91 0 L 91 -1 L 90 -1 L 90 0 Z M 90 40 L 90 41 L 91 41 L 91 40 L 90 40 Z M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 90 0 L 89 0 L 89 40 L 90 40 L 91 40 L 91 0 L 90 0 Z M 90 40 L 90 39 L 4 39 L 4 40 L 4 41 L 90 41 L 90 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"disabled"} />}</div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"disabled"} />}</div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 38,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={38} height={40} viewBox="0 0 38 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 38,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 38 0 L 39 0 L 39 -1 L 38 -1 L 38 0 Z M 38 40 L 38 41 L 39 41 L 39 40 L 38 40 Z M 4 0 L 4 1 L 38 1 L 38 0 L 38 -1 L 4 -1 L 4 0 Z M 38 0 L 37 0 L 37 40 L 38 40 L 39 40 L 39 0 L 38 0 Z M 38 40 L 38 39 L 4 39 L 4 40 L 4 41 L 38 41 L 38 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 90,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={90} height={40} viewBox="0 0 90 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 90,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 90 0 L 91 0 L 91 -1 L 90 -1 L 90 0 Z M 90 40 L 90 41 L 91 41 L 91 40 L 90 40 Z M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 90 0 L 89 0 L 89 40 L 90 40 L 91 40 L 91 0 L 90 0 Z M 90 40 L 90 39 L 4 39 L 4 40 L 4 41 L 90 41 L 90 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"disabled"} />}</div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"disabled"} />}</div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-1-selected)",
        borderRight: "1px solid var(--neutral-stroke-1-selected)",
        borderBottom: "1px solid var(--neutral-stroke-1-selected)",
        borderLeft: "1px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-selected)",
            }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-1-selected)",
        borderRight: "1px solid var(--neutral-stroke-1-selected)",
        borderBottom: "1px solid var(--neutral-stroke-1-selected)",
        borderLeft: "1px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.pxFilled24 ?? <Placeholder4 size={"24"} theme={"filled"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-1-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-pressed)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              color: "var(--neutral-foreground-1-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "7px 7px 7px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 38,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={38} height={40} viewBox="0 0 38 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 38,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 38 0 L 39 0 L 39 -1 L 38 -1 L 38 0 Z M 38 40 L 38 41 L 39 41 L 39 40 L 38 40 Z M 4 0 L 4 1 L 38 1 L 38 0 L 38 -1 L 4 -1 L 4 0 Z M 38 0 L 37 0 L 37 40 L 38 40 L 39 40 L 39 0 L 38 0 Z M 38 40 L 38 39 L 4 39 L 4 40 L 4 41 L 38 41 L 38 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 40,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 40,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "7px 15px 7px 15px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxRegular24 ?? <Placeholder4 size={"24"} theme={"regular"} />}</div>
        )}
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
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 90,
          height: 40,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={90} height={40} viewBox="0 0 90 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 90,
            height: 40,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 90 0 L 91 0 L 91 -1 L 90 -1 L 90 0 Z M 90 40 L 90 41 L 91 41 L 91 40 L 90 40 Z M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 90 0 L 89 0 L 89 40 L 90 40 L 91 40 L 91 0 L 90 0 Z M 90 40 L 90 39 L 4 39 L 4 40 L 4 41 L 90 41 L 90 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 4.5,
            width: 8,
            height: 4.5,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 0.146 0.146 C 0.342 -0.049 0.658 -0.049 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.354 4.354 C 4.158 4.549 3.842 4.549 3.646 4.354 L 0.146 0.854 C -0.049 0.658 -0.049 0.342 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 4.5,
            width: 8,
            height: 4.5,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 0.146 0.146 C 0.342 -0.049 0.658 -0.049 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.354 4.354 C 4.158 4.549 3.842 4.549 3.646 4.354 L 0.146 0.854 C -0.049 0.658 -0.049 0.342 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-brand-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-brand-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-hover)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-brand-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 30,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={30} height={32} viewBox="0 0 30 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 30,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 30 0 L 31 0 L 31 -1 L 30 -1 L 30 0 Z M 30 32 L 30 33 L 31 33 L 31 32 L 30 32 Z M 4 0 L 4 1 L 30 1 L 30 0 L 30 -1 L 4 -1 L 4 0 Z M 30 0 L 29 0 L 29 32 L 30 32 L 31 32 L 31 0 L 30 0 Z M 30 32 L 30 31 L 4 31 L 4 32 L 4 33 L 30 33 L 30 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 74,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={74} height={32} viewBox="0 0 74 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 74,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 74 0 L 75 0 L 75 -1 L 74 -1 L 74 0 Z M 74 32 L 74 33 L 75 33 L 75 32 L 74 32 Z M 4 0 L 4 1 L 74 1 L 74 0 L 74 -1 L 4 -1 L 4 0 Z M 74 0 L 73 0 L 73 32 L 74 32 L 75 32 L 75 0 L 74 0 Z M 74 32 L 74 31 L 4 31 L 4 32 L 4 33 L 74 33 L 74 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-selected)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-brand-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-pressed)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-brand-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-hover)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-hover)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-brand-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 30,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={30} height={32} viewBox="0 0 30 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 30,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 30 0 L 31 0 L 31 -1 L 30 -1 L 30 0 Z M 30 32 L 30 33 L 31 33 L 31 32 L 30 32 Z M 4 0 L 4 1 L 30 1 L 30 0 L 30 -1 L 4 -1 L 4 0 Z M 30 0 L 29 0 L 29 32 L 30 32 L 31 32 L 31 0 L 30 0 Z M 30 32 L 30 31 L 4 31 L 4 32 L 4 33 L 30 33 L 30 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 74,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={74} height={32} viewBox="0 0 74 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 74,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 74 0 L 75 0 L 75 -1 L 74 -1 L 74 0 Z M 74 32 L 74 33 L 75 33 L 75 32 L 74 32 Z M 4 0 L 4 1 L 74 1 L 74 0 L 74 -1 L 4 -1 L 4 0 Z M 74 0 L 73 0 L 73 32 L 74 32 L 75 32 L 75 0 L 74 0 Z M 74 32 L 74 31 L 4 31 L 4 32 L 4 33 L 74 33 L 74 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 5px 0px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          display: "flex",
          flexDirection: "row",
          padding: "0px 1px 0px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <Chevron2
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}
            direction={"down"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 5px 0px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          display: "flex",
          flexDirection: "row",
          padding: "0px 1px 0px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <Chevron2
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}
            direction={"down"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "2px solid var(--neutral-stroke-1-selected)",
        borderRight: "2px solid var(--neutral-stroke-1-selected)",
        borderBottom: "2px solid var(--neutral-stroke-1-selected)",
        borderLeft: "2px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "2px solid var(--neutral-stroke-1-selected)",
        borderRight: "2px solid var(--neutral-stroke-1-selected)",
        borderBottom: "2px solid var(--neutral-stroke-1-selected)",
        borderLeft: "2px solid var(--neutral-stroke-1-selected)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-pressed)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body73 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body74 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-hover)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body75 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body76 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 30,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={30} height={32} viewBox="0 0 30 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 30,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 30 0 L 31 0 L 31 -1 L 30 -1 L 30 0 Z M 30 32 L 30 33 L 31 33 L 31 32 L 30 32 Z M 4 0 L 4 1 L 30 1 L 30 0 L 30 -1 L 4 -1 L 4 0 Z M 30 0 L 29 0 L 29 32 L 30 32 L 31 32 L 31 0 L 30 0 Z M 30 32 L 30 31 L 4 31 L 4 32 L 4 33 L 30 33 L 30 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body77 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 74,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={74} height={32} viewBox="0 0 74 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 74,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 74 0 L 75 0 L 75 -1 L 74 -1 L 74 0 Z M 74 32 L 74 33 L 75 33 L 75 32 L 74 32 Z M 4 0 L 4 1 L 74 1 L 74 0 L 74 -1 L 4 -1 L 4 0 Z M 74 0 L 73 0 L 73 32 L 74 32 L 75 32 L 75 0 L 74 0 Z M 74 32 L 74 31 L 4 31 L 4 32 L 4 33 L 74 33 L 74 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body78 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          display: "flex",
          flexDirection: "row",
          padding: "0px 1px 0px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <Chevron2
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}
            direction={"down"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
    </div>
  );
  const __body79 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          display: "flex",
          flexDirection: "row",
          padding: "0px 1px 0px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <Chevron2
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}
            direction={"down"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
    </div>
  );
  const __body80 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body81 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
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
        {props.icon && (
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
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
        )}
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body82 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body83 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body84 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body85 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body86 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 30,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={30} height={32} viewBox="0 0 30 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 30,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 30 0 L 31 0 L 31 -1 L 30 -1 L 30 0 Z M 30 32 L 30 33 L 31 33 L 31 32 L 30 32 Z M 4 0 L 4 1 L 30 1 L 30 0 L 30 -1 L 4 -1 L 4 0 Z M 30 0 L 29 0 L 29 32 L 30 32 L 31 32 L 31 0 L 30 0 Z M 30 32 L 30 31 L 4 31 L 4 32 L 4 33 L 30 33 L 30 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body87 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 74,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={74} height={32} viewBox="0 0 74 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 74,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 74 0 L 75 0 L 75 -1 L 74 -1 L 74 0 Z M 74 32 L 74 33 L 75 33 L 75 32 L 74 32 Z M 4 0 L 4 1 L 74 1 L 74 0 L 74 -1 L 4 -1 L 4 0 Z M 74 0 L 73 0 L 73 32 L 74 32 L 75 32 L 75 0 L 74 0 Z M 74 32 L 74 31 L 4 31 L 4 32 L 4 33 L 74 33 L 74 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body88 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 5px 0px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          display: "flex",
          flexDirection: "row",
          padding: "0px 1px 0px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <Chevron2
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}
            direction={"down"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
    </div>
  );
  const __body89 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: 32,
        borderRadius: "0px 4px 4px 0px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 5px 0px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-none-2) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          display: "flex",
          flexDirection: "row",
          padding: "0px 1px 0px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <Chevron2
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}
            direction={"down"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
    </div>
  );
  const __body90 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-1-selected)",
        borderRight: "1px solid var(--neutral-stroke-1-selected)",
        borderBottom: "1px solid var(--neutral-stroke-1-selected)",
        borderLeft: "1px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body91 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-1-selected)",
        borderRight: "1px solid var(--neutral-stroke-1-selected)",
        borderBottom: "1px solid var(--neutral-stroke-1-selected)",
        borderLeft: "1px solid var(--neutral-stroke-1-selected)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-selected)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body92 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-pressed)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body93 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body94 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body95 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-hover)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body96 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "5px 5px 5px 5px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 30,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={30} height={32} viewBox="0 0 30 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 30,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 30 0 L 31 0 L 31 -1 L 30 -1 L 30 0 Z M 30 32 L 30 33 L 31 33 L 31 32 L 30 32 Z M 4 0 L 4 1 L 30 1 L 30 0 L 30 -1 L 4 -1 L 4 0 Z M 30 0 L 29 0 L 29 32 L 30 32 L 31 32 L 31 0 L 30 0 Z M 30 32 L 30 31 L 4 31 L 4 32 L 4 33 L 30 33 L 30 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body97 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 32,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 32,
        maxWidth: null,
        maxHeight: 32,
        borderRadius: "4px 0px 0px 4px",
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
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 74,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={74} height={32} viewBox="0 0 74 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 74,
            height: 32,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 74 0 L 75 0 L 75 -1 L 74 -1 L 74 0 Z M 74 32 L 74 33 L 75 33 L 75 32 L 74 32 Z M 4 0 L 4 1 L 74 1 L 74 0 L 74 -1 L 4 -1 L 4 0 Z M 74 0 L 73 0 L 73 32 L 74 32 L 75 32 L 75 0 L 74 0 Z M 74 32 L 74 31 L 4 31 L 4 32 L 4 33 L 74 33 L 74 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body98 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"disabled"} />}</div>
    </div>
  );
  const __body99 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"disabled"} />}</div>
    </div>
  );
  const __body100 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body101 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-brand-selected)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body102 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body103 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-brand-pressed)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body104 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-hover)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body105 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-brand-hover)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body106 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 22,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 22,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 22 0 L 23 0 L 23 -1 L 22 -1 L 22 0 Z M 22 24 L 22 25 L 23 25 L 23 24 L 22 24 Z M 4 0 L 4 1 L 22 1 L 22 0 L 22 -1 L 4 -1 L 4 0 Z M 22 0 L 21 0 L 21 24 L 22 24 L 23 24 L 23 0 L 22 0 Z M 22 24 L 22 23 L 4 23 L 4 24 L 4 25 L 22 25 L 22 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body107 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 59,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={59} height={24} viewBox="0 0 59 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 59,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 59 0 L 60 0 L 60 -1 L 59 -1 L 59 0 Z M 59 24 L 59 25 L 60 25 L 60 24 L 59 24 Z M 4 0 L 4 1 L 59 1 L 59 0 L 59 -1 L 4 -1 L 4 0 Z M 59 0 L 58 0 L 58 24 L 59 24 L 60 24 L 60 0 L 59 0 Z M 59 24 L 59 23 L 4 23 L 4 24 L 4 25 L 59 25 L 59 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"transparent"} state={"rest"} />}</div>
    </div>
  );
  const __body108 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"disabled"} />}</div>
    </div>
  );
  const __body109 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"disabled"} />}</div>
    </div>
  );
  const __body110 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body111 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-brand-selected)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body112 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body113 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-brand-pressed)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body114 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-brand-hover)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body115 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-brand-hover)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body116 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 22,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 22,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 22 0 L 23 0 L 23 -1 L 22 -1 L 22 0 Z M 22 24 L 22 25 L 23 25 L 23 24 L 22 24 Z M 4 0 L 4 1 L 22 1 L 22 0 L 22 -1 L 4 -1 L 4 0 Z M 22 0 L 21 0 L 21 24 L 22 24 L 23 24 L 23 0 L 22 0 Z M 22 24 L 22 23 L 4 23 L 4 24 L 4 25 L 22 25 L 22 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body117 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 59,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={59} height={24} viewBox="0 0 59 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 59,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 59 0 L 60 0 L 60 -1 L 59 -1 L 59 0 Z M 59 24 L 59 25 L 60 25 L 60 24 L 59 24 Z M 4 0 L 4 1 L 59 1 L 59 0 L 59 -1 L 4 -1 L 4 0 Z M 59 0 L 58 0 L 58 24 L 59 24 L 60 24 L 60 0 L 59 0 Z M 59 24 L 59 23 L 4 23 L 4 24 L 4 25 L 59 25 L 59 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"subtle"} state={"rest"} />}</div>
    </div>
  );
  const __body118 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"disabled"} />}</div>
    </div>
  );
  const __body119 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"disabled"} />}</div>
    </div>
  );
  const __body120 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "2px solid var(--neutral-stroke-1-selected)",
        borderRight: "2px solid var(--neutral-stroke-1-selected)",
        borderBottom: "2px solid var(--neutral-stroke-1-selected)",
        borderLeft: "2px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body121 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "2px solid var(--neutral-stroke-1-selected)",
        borderRight: "2px solid var(--neutral-stroke-1-selected)",
        borderBottom: "2px solid var(--neutral-stroke-1-selected)",
        borderLeft: "2px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "2px 8px 2px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-selected)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body122 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-pressed)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body123 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body124 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-hover)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body125 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-hover)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body126 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 22,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 22,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 22 0 L 23 0 L 23 -1 L 22 -1 L 22 0 Z M 22 24 L 22 25 L 23 25 L 23 24 L 22 24 Z M 4 0 L 4 1 L 22 1 L 22 0 L 22 -1 L 4 -1 L 4 0 Z M 22 0 L 21 0 L 21 24 L 22 24 L 23 24 L 23 0 L 22 0 Z M 22 24 L 22 23 L 4 23 L 4 24 L 4 25 L 22 25 L 22 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body127 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 59,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={59} height={24} viewBox="0 0 59 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 59,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 59 0 L 60 0 L 60 -1 L 59 -1 L 59 0 Z M 59 24 L 59 25 L 60 25 L 60 24 L 59 24 Z M 4 0 L 4 1 L 59 1 L 59 0 L 59 -1 L 4 -1 L 4 0 Z M 59 0 L 58 0 L 58 24 L 59 24 L 60 24 L 60 0 L 59 0 Z M 59 24 L 59 23 L 4 23 L 4 24 L 4 25 L 59 25 L 59 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"outline"} state={"rest"} />}</div>
    </div>
  );
  const __body128 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"disabled"} />}</div>
    </div>
  );
  const __body129 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"disabled"} />}</div>
    </div>
  );
  const __body130 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body131 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body132 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body133 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body134 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body135 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body136 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 22,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 22,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 22 0 L 23 0 L 23 -1 L 22 -1 L 22 0 Z M 22 24 L 22 25 L 23 25 L 23 24 L 22 24 Z M 4 0 L 4 1 L 22 1 L 22 0 L 22 -1 L 4 -1 L 4 0 Z M 22 0 L 21 0 L 21 24 L 22 24 L 23 24 L 23 0 L 22 0 Z M 22 24 L 22 23 L 4 23 L 4 24 L 4 25 L 22 25 L 22 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body137 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--brand-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 59,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={59} height={24} viewBox="0 0 59 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 59,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 59 0 L 60 0 L 60 -1 L 59 -1 L 59 0 Z M 59 24 L 59 25 L 60 25 L 60 24 L 59 24 Z M 4 0 L 4 1 L 59 1 L 59 0 L 59 -1 L 4 -1 L 4 0 Z M 59 0 L 58 0 L 58 24 L 59 24 L 60 24 L 60 0 L 59 0 Z M 59 24 L 59 23 L 4 23 L 4 24 L 4 25 L 59 25 L 59 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"primary"} state={"rest"} />}</div>
    </div>
  );
  const __body138 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"disabled"} />}</div>
    </div>
  );
  const __body139 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-disabled-rest)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"disabled"} />}</div>
    </div>
  );
  const __body140 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-1-selected)",
        borderRight: "1px solid var(--neutral-stroke-1-selected)",
        borderBottom: "1px solid var(--neutral-stroke-1-selected)",
        borderLeft: "1px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-selected)",
            }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body141 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-selected)",
        borderTop: "1px solid var(--neutral-stroke-1-selected)",
        borderRight: "1px solid var(--neutral-stroke-1-selected)",
        borderBottom: "1px solid var(--neutral-stroke-1-selected)",
        borderLeft: "1px solid var(--neutral-stroke-1-selected)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.pxFilled20 ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-selected)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body142 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-pressed)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body143 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-pressed)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body144 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-hover)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body145 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-hover)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-hover)",
              flexShrink: 0,
            }}>{props.label}</span>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body146 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        minHeight: 24,
        maxWidth: 24,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 2px 1px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
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
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 22,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 22,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 22 0 L 23 0 L 23 -1 L 22 -1 L 22 0 Z M 22 24 L 22 25 L 23 25 L 23 24 L 22 24 Z M 4 0 L 4 1 L 22 1 L 22 0 L 22 -1 L 4 -1 L 4 0 Z M 22 0 L 21 0 L 21 24 L 22 24 L 23 24 L 23 0 L 22 0 Z M 22 24 L 22 23 L 4 23 L 4 24 L 4 25 L 22 25 L 22 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
    </div>
  );
  const __body147 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: null,
      maxHeight: 24,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minHeight: 24,
        maxWidth: null,
        maxHeight: 24,
        borderRadius: "4px 0px 0px 4px",
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "1px 7px 1px 7px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxRegular20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
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
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
            }}>{props.label}</span>
          </div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 59,
          height: 24,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={59} height={24} viewBox="0 0 59 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 59,
            height: 24,
            overflow: "hidden",
            borderRadius: "4px 0px 0px 4px",
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 59 0 L 60 0 L 60 -1 L 59 -1 L 59 0 Z M 59 24 L 59 25 L 60 25 L 60 24 L 59 24 Z M 4 0 L 4 1 L 59 1 L 59 0 L 59 -1 L 4 -1 L 4 0 Z M 59 0 L 58 0 L 58 24 L 59 24 L 60 24 L 60 0 L 59 0 Z M 59 24 L 59 23 L 4 23 L 4 24 L 4 25 L 59 25 L 59 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <SecondaryAction style2={"secondary (default)"} state={"rest"} />}</div>
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
    "style2=subtle|state=rest|size=lg|layout=icon only": __body18,
    // figma: Style=Subtle, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=subtle|state=rest|size=lg|layout=icon and label (default)": __body19,
    // figma: Style=Outline, State=Disabled, Size=Large, Layout=Icon only
    "style2=outline|state=disabled|size=lg|layout=icon only": __body20,
    // figma: Style=Outline, State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=disabled|size=lg|layout=icon and label (default)": __body21,
    // figma: Style=Outline, State=Selected, Size=Large, Layout=Icon only
    "style2=outline|state=selected|size=lg|layout=icon only": __body22,
    // figma: Style=Outline, State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=selected|size=lg|layout=icon and label (default)": __body23,
    // figma: Style=Outline, State=Pressed, Size=Large, Layout=Icon only
    "style2=outline|state=pressed|size=lg|layout=icon only": __body24,
    // figma: Style=Outline, State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=pressed|size=lg|layout=icon and label (default)": __body25,
    // figma: Style=Outline, State=Hover, Size=Large, Layout=Icon only
    "style2=outline|state=hover|size=lg|layout=icon only": __body26,
    // figma: Style=Outline, State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=hover|size=lg|layout=icon and label (default)": __body27,
    // figma: Style=Outline, State=Rest, Size=Large, Layout=Icon only
    "style2=outline|state=rest|size=lg|layout=icon only": __body28,
    // figma: Style=Outline, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=rest|size=lg|layout=icon and label (default)": __body29,
    // figma: Style=Primary, State=Disabled, Size=Large, Layout=Icon only
    "style2=primary|state=disabled|size=lg|layout=icon only": __body30,
    // figma: Style=Primary, State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=disabled|size=lg|layout=icon and label (default)": __body31,
    // figma: Style=Primary, State=Selected, Size=Large, Layout=Icon only
    "style2=primary|state=selected|size=lg|layout=icon only": __body32,
    // figma: Style=Primary, State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=selected|size=lg|layout=icon and label (default)": __body33,
    // figma: Style=Primary, State=Pressed, Size=Large, Layout=Icon only
    "style2=primary|state=pressed|size=lg|layout=icon only": __body34,
    // figma: Style=Primary, State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=pressed|size=lg|layout=icon and label (default)": __body35,
    // figma: Style=Primary, State=Hover, Size=Large, Layout=Icon only
    "style2=primary|state=hover|size=lg|layout=icon only": __body36,
    // figma: Style=Primary, State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=hover|size=lg|layout=icon and label (default)": __body37,
    // figma: Style=Primary, State=Rest, Size=Large, Layout=Icon only
    "style2=primary|state=rest|size=lg|layout=icon only": __body38,
    // figma: Style=Primary, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=rest|size=lg|layout=icon and label (default)": __body39,
    // figma: Style=Secondary (Default), State=Disabled, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=disabled|size=lg|layout=icon only": __body40,
    // figma: Style=Secondary (Default), State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=disabled|size=lg|layout=icon and label (default)": __body41,
    // figma: Style=Secondary (Default), State=Selected, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=selected|size=lg|layout=icon only": __body42,
    // figma: Style=Secondary (Default), State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=selected|size=lg|layout=icon and label (default)": __body43,
    // figma: Style=Secondary (Default), State=Pressed, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=pressed|size=lg|layout=icon only": __body44,
    // figma: Style=Secondary (Default), State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=pressed|size=lg|layout=icon and label (default)": __body45,
    // figma: Style=Secondary (Default), State=Hover, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=hover|size=lg|layout=icon only": __body46,
    // figma: Style=Secondary (Default), State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=hover|size=lg|layout=icon and label (default)": __body47,
    // figma: Style=Secondary (Default), State=Rest, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=rest|size=lg|layout=icon only": __body48,
    // figma: Style=Secondary (Default), State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=rest|size=lg|layout=icon and label (default)": __body49,
    // figma: Style=Transparent, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=disabled|size=medium (default)|layout=icon only": __body50,
    // figma: Style=Transparent, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=disabled|size=medium (default)|layout=icon and label (default)": __body51,
    // figma: Style=Transparent, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=selected|size=medium (default)|layout=icon only": __body52,
    // figma: Style=Transparent, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=selected|size=medium (default)|layout=icon and label (default)": __body53,
    // figma: Style=Transparent, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=pressed|size=medium (default)|layout=icon only": __body54,
    // figma: Style=Transparent, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=pressed|size=medium (default)|layout=icon and label (default)": __body55,
    // figma: Style=Transparent, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=hover|size=medium (default)|layout=icon only": __body56,
    // figma: Style=Transparent, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=hover|size=medium (default)|layout=icon and label (default)": __body57,
    // figma: Style=Transparent, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=rest|size=medium (default)|layout=icon only": __body58,
    // figma: Style=Transparent, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=rest|size=medium (default)|layout=icon and label (default)": __body59,
    // figma: Style=Subtle, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=disabled|size=medium (default)|layout=icon only": __body50,
    // figma: Style=Subtle, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=disabled|size=medium (default)|layout=icon and label (default)": __body51,
    // figma: Style=Subtle, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=selected|size=medium (default)|layout=icon only": __body60,
    // figma: Style=Subtle, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=selected|size=medium (default)|layout=icon and label (default)": __body61,
    // figma: Style=Subtle, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=pressed|size=medium (default)|layout=icon only": __body62,
    // figma: Style=Subtle, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=pressed|size=medium (default)|layout=icon and label (default)": __body63,
    // figma: Style=Subtle, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=hover|size=medium (default)|layout=icon only": __body64,
    // figma: Style=Subtle, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=hover|size=medium (default)|layout=icon and label (default)": __body65,
    // figma: Style=Subtle, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=rest|size=medium (default)|layout=icon only": __body66,
    // figma: Style=Subtle, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=rest|size=medium (default)|layout=icon and label (default)": __body67,
    // figma: Style=Outline, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=disabled|size=medium (default)|layout=icon only": __body68,
    // figma: Style=Outline, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=disabled|size=medium (default)|layout=icon and label (default)": __body69,
    // figma: Style=Outline, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=selected|size=medium (default)|layout=icon only": __body70,
    // figma: Style=Outline, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=selected|size=medium (default)|layout=icon and label (default)": __body71,
    // figma: Style=Outline, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=pressed|size=medium (default)|layout=icon only": __body72,
    // figma: Style=Outline, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=pressed|size=medium (default)|layout=icon and label (default)": __body73,
    // figma: Style=Outline, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=hover|size=medium (default)|layout=icon only": __body74,
    // figma: Style=Outline, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=hover|size=medium (default)|layout=icon and label (default)": __body75,
    // figma: Style=Outline, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=rest|size=medium (default)|layout=icon only": __body76,
    // figma: Style=Outline, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=rest|size=medium (default)|layout=icon and label (default)": __body77,
    // figma: Style=Primary, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=disabled|size=medium (default)|layout=icon only": __body78,
    // figma: Style=Primary, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=disabled|size=medium (default)|layout=icon and label (default)": __body79,
    // figma: Style=Primary, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=selected|size=medium (default)|layout=icon only": __body80,
    // figma: Style=Primary, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=selected|size=medium (default)|layout=icon and label (default)": __body81,
    // figma: Style=Primary, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=pressed|size=medium (default)|layout=icon only": __body82,
    // figma: Style=Primary, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=pressed|size=medium (default)|layout=icon and label (default)": __body83,
    // figma: Style=Primary, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=hover|size=medium (default)|layout=icon only": __body84,
    // figma: Style=Primary, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=hover|size=medium (default)|layout=icon and label (default)": __body85,
    // figma: Style=Primary, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=rest|size=medium (default)|layout=icon only": __body86,
    // figma: Style=Primary, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=rest|size=medium (default)|layout=icon and label (default)": __body87,
    // figma: Style=Secondary (Default), State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=disabled|size=medium (default)|layout=icon only": __body88,
    // figma: Style=Secondary (Default), State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=disabled|size=medium (default)|layout=icon and label (default)": __body89,
    // figma: Style=Secondary (Default), State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=selected|size=medium (default)|layout=icon only": __body90,
    // figma: Style=Secondary (Default), State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=selected|size=medium (default)|layout=icon and label (default)": __body91,
    // figma: Style=Secondary (Default), State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=pressed|size=medium (default)|layout=icon only": __body92,
    // figma: Style=Secondary (Default), State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=pressed|size=medium (default)|layout=icon and label (default)": __body93,
    // figma: Style=Secondary (Default), State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=hover|size=medium (default)|layout=icon only": __body94,
    // figma: Style=Secondary (Default), State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=hover|size=medium (default)|layout=icon and label (default)": __body95,
    // figma: Style=Secondary (Default), State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=rest|size=medium (default)|layout=icon only": __body96,
    // figma: Style=Secondary (Default), State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=rest|size=medium (default)|layout=icon and label (default)": __body97,
    // figma: Style=Transparent, State=Disabled, Size=Small, Layout=Icon only
    "style2=transparent|state=disabled|size=sm|layout=icon only": __body98,
    // figma: Style=Transparent, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=disabled|size=sm|layout=icon and label (default)": __body99,
    // figma: Style=Transparent, State=Selected, Size=Small, Layout=Icon only
    "style2=transparent|state=selected|size=sm|layout=icon only": __body100,
    // figma: Style=Transparent, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=selected|size=sm|layout=icon and label (default)": __body101,
    // figma: Style=Transparent, State=Pressed, Size=Small, Layout=Icon only
    "style2=transparent|state=pressed|size=sm|layout=icon only": __body102,
    // figma: Style=Transparent, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=pressed|size=sm|layout=icon and label (default)": __body103,
    // figma: Style=Transparent, State=Hover, Size=Small, Layout=Icon only
    "style2=transparent|state=hover|size=sm|layout=icon only": __body104,
    // figma: Style=Transparent, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=hover|size=sm|layout=icon and label (default)": __body105,
    // figma: Style=Transparent, State=Rest, Size=Small, Layout=Icon only
    "style2=transparent|state=rest|size=sm|layout=icon only": __body106,
    // figma: Style=Transparent, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=rest|size=sm|layout=icon and label (default)": __body107,
    // figma: Style=Subtle, State=Disabled, Size=Small, Layout=Icon only
    "style2=subtle|state=disabled|size=sm|layout=icon only": __body108,
    // figma: Style=Subtle, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=disabled|size=sm|layout=icon and label (default)": __body109,
    // figma: Style=Subtle, State=Selected, Size=Small, Layout=Icon only
    "style2=subtle|state=selected|size=sm|layout=icon only": __body110,
    // figma: Style=Subtle, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=selected|size=sm|layout=icon and label (default)": __body111,
    // figma: Style=Subtle, State=Pressed, Size=Small, Layout=Icon only
    "style2=subtle|state=pressed|size=sm|layout=icon only": __body112,
    // figma: Style=Subtle, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=pressed|size=sm|layout=icon and label (default)": __body113,
    // figma: Style=Subtle, State=Hover, Size=Small, Layout=Icon only
    "style2=subtle|state=hover|size=sm|layout=icon only": __body114,
    // figma: Style=Subtle, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=hover|size=sm|layout=icon and label (default)": __body115,
    // figma: Style=Subtle, State=Rest, Size=Small, Layout=Icon only
    "style2=subtle|state=rest|size=sm|layout=icon only": __body116,
    // figma: Style=Subtle, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=rest|size=sm|layout=icon and label (default)": __body117,
    // figma: Style=Outline, State=Disabled, Size=Small, Layout=Icon only
    "style2=outline|state=disabled|size=sm|layout=icon only": __body118,
    // figma: Style=Outline, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=disabled|size=sm|layout=icon and label (default)": __body119,
    // figma: Style=Outline, State=Selected, Size=Small, Layout=Icon only
    "style2=outline|state=selected|size=sm|layout=icon only": __body120,
    // figma: Style=Outline, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=selected|size=sm|layout=icon and label (default)": __body121,
    // figma: Style=Outline, State=Pressed, Size=Small, Layout=Icon only
    "style2=outline|state=pressed|size=sm|layout=icon only": __body122,
    // figma: Style=Outline, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=pressed|size=sm|layout=icon and label (default)": __body123,
    // figma: Style=Outline, State=Hover, Size=Small, Layout=Icon only
    "style2=outline|state=hover|size=sm|layout=icon only": __body124,
    // figma: Style=Outline, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=hover|size=sm|layout=icon and label (default)": __body125,
    // figma: Style=Outline, State=Rest, Size=Small, Layout=Icon only
    "style2=outline|state=rest|size=sm|layout=icon only": __body126,
    // figma: Style=Outline, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=rest|size=sm|layout=icon and label (default)": __body127,
    // figma: Style=Primary, State=Disabled, Size=Small, Layout=Icon only
    "style2=primary|state=disabled|size=sm|layout=icon only": __body128,
    // figma: Style=Primary, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=disabled|size=sm|layout=icon and label (default)": __body129,
    // figma: Style=Primary, State=Selected, Size=Small, Layout=Icon only
    "style2=primary|state=selected|size=sm|layout=icon only": __body130,
    // figma: Style=Primary, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=selected|size=sm|layout=icon and label (default)": __body131,
    // figma: Style=Primary, State=Pressed, Size=Small, Layout=Icon only
    "style2=primary|state=pressed|size=sm|layout=icon only": __body132,
    // figma: Style=Primary, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=pressed|size=sm|layout=icon and label (default)": __body133,
    // figma: Style=Primary, State=Hover, Size=Small, Layout=Icon only
    "style2=primary|state=hover|size=sm|layout=icon only": __body134,
    // figma: Style=Primary, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=hover|size=sm|layout=icon and label (default)": __body135,
    // figma: Style=Primary, State=Rest, Size=Small, Layout=Icon only
    "style2=primary|state=rest|size=sm|layout=icon only": __body136,
    // figma: Style=Primary, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=rest|size=sm|layout=icon and label (default)": __body137,
    // figma: Style=Secondary (Default), State=Disabled, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=disabled|size=sm|layout=icon only": __body138,
    // figma: Style=Secondary (Default), State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=disabled|size=sm|layout=icon and label (default)": __body139,
    // figma: Style=Secondary (Default), State=Selected, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=selected|size=sm|layout=icon only": __body140,
    // figma: Style=Secondary (Default), State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=selected|size=sm|layout=icon and label (default)": __body141,
    // figma: Style=Secondary (Default), State=Pressed, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=pressed|size=sm|layout=icon only": __body142,
    // figma: Style=Secondary (Default), State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=pressed|size=sm|layout=icon and label (default)": __body143,
    // figma: Style=Secondary (Default), State=Hover, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=hover|size=sm|layout=icon only": __body144,
    // figma: Style=Secondary (Default), State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=hover|size=sm|layout=icon and label (default)": __body145,
    // figma: Style=Secondary (Default), State=Rest, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=rest|size=sm|layout=icon only": __body146,
    // figma: Style=Secondary (Default), State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=rest|size=sm|layout=icon and label (default)": __body147,
  };
  return (__impls[__vkey(props)] ?? __body39)();
}
export default SplitButton;
