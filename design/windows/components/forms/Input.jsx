import { __slateNorm } from '../internal/slateProps.jsx';
import { Calendar2 } from '../internal/Calendar2.jsx';
import { Chevron } from '../internal/Chevron.jsx';
import { ErrorCircle } from '../internal/ErrorCircle.jsx';

// figma node: 9115:8452 Input (84 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state);

export function Input(_p = {}) {
  _p = __slateNorm(_p, {"style2":["outline","filled darker","filled lighter","underline"],"size":["sm","medium (default)","lg"],"state":["rest","hover","pressed","focus","error","disabled","read only"]});
  const props = { ..._p, style2: _p.style2 ?? "outline", size: _p.size ?? "sm", state: _p.state ?? "rest", iconAfter1: _p.iconAfter1 ?? false, iconAfter2: _p.iconAfter2 ?? true, iconBefore: _p.iconBefore ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 24,
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
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
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={77} height={2} viewBox="0 0 77 2" fill="none" style={{
        position: "absolute",
        left: 102,
        top: 22,
        width: 77,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 77 0 L 76.889 2 L 0.102 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
              flexGrow: 1,
              alignSelf: "stretch",
            }}>{props.placeholder ?? "Placeholder text"}</span>
          </div>
        </div>
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
              flexGrow: 1,
              alignSelf: "stretch",
            }}>{props.placeholder ?? "Placeholder text"}</span>
          </div>
        </div>
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
              flexGrow: 1,
              alignSelf: "stretch",
            }}>{props.placeholder ?? "Placeholder text"}</span>
          </div>
        </div>
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
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
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
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
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={77} height={2} viewBox="0 0 77 2" fill="none" style={{
        position: "absolute",
        left: 102,
        top: 30,
        width: 77,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 77 0 L 76.889 2 L 0.102 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={77} height={2} viewBox="0 0 77 2" fill="none" style={{
        position: "absolute",
        left: 102,
        top: 38,
        width: 77,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 77 0 L 76.889 2 L 0.102 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 280,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: 280,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 1,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={77} height={2} viewBox="0 0 77 2" fill="none" style={{
        position: "absolute",
        left: 102,
        top: 22,
        width: 77,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 77 0 L 76.889 2 L 0.102 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 1,
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--status-danger-stroke-2-rest)",
      }} />
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
              flexGrow: 1,
              alignSelf: "stretch",
            }}>{props.placeholder ?? "Placeholder text"}</span>
          </div>
        </div>
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-small) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore16pxRegular ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 24,
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "6px 2px 6px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
              flexGrow: 1,
              alignSelf: "stretch",
            }}>{props.placeholder ?? "Placeholder text"}</span>
          </div>
        </div>
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 6px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-small) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <ErrorCircle size={"16"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter116pxRegular ?? <Chevron direction={"down"} size={"16"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: 280,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 1,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={77} height={2} viewBox="0 0 77 2" fill="none" style={{
        position: "absolute",
        left: 102,
        top: 30,
        width: 77,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 77 0 L 76.889 2 L 0.102 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 1,
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--status-danger-stroke-2-rest)",
      }} />
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: 280,
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
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 10px 0px 10px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore20pxRegular ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 32,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 56,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 10px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-medium) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter220pxRegular ?? <ErrorCircle size={"20"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter120pxRegular ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: 280,
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
        height: 40,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: 280,
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
        height: 40,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: 280,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 40,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: 1,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={77} height={2} viewBox="0 0 77 2" fill="none" style={{
        position: "absolute",
        left: 102,
        top: 38,
        width: 77,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 77 0 L 76.889 2 L 0.102 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: 280,
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
        height: 40,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 280,
        height: 2,
        borderRadius: 1,
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: 280,
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
        height: 40,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--status-danger-stroke-2-rest)",
      }} />
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: 280,
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
        height: 40,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: 280,
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
        height: 40,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
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
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-input-container-default) * 1px)",
        paddingRight: "calc(var(--spacing-input-container-default) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 12px 0px 12px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-input-content-large) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          flexGrow: 1,
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconBefore24pxRegular ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            height: 34,
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
        {props.iconAfter1 && (
        <div style={{
          position: "relative",
          width: 66,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 12px 8px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-input-content-large) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconAfter2 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--status-danger-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <ErrorCircle size={"24"} theme={"regular"} />}</div>
          )}
          {props.iconAfter1 && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter124pxRegular ?? <Chevron direction={"down"} size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __impls = {
    // figma: Style=Outline, Size=Small, State=Rest
    "style2=outline|size=sm|state=rest": __body0,
    // figma: Style=Filled darker, Size=Small, State=Rest
    "style2=filled darker|size=sm|state=rest": __body1,
    // figma: Style=Filled lighter, Size=Small, State=Rest
    "style2=filled lighter|size=sm|state=rest": __body2,
    // figma: Style=Outline, Size=Small, State=Hover
    "style2=outline|size=sm|state=hover": __body3,
    // figma: Style=Filled darker, Size=Small, State=Hover
    "style2=filled darker|size=sm|state=hover": __body1,
    // figma: Style=Filled lighter, Size=Small, State=Hover
    "style2=filled lighter|size=sm|state=hover": __body2,
    // figma: Style=Outline, Size=Small, State=Pressed
    "style2=outline|size=sm|state=pressed": __body4,
    // figma: Style=Filled darker, Size=Small, State=Pressed
    "style2=filled darker|size=sm|state=pressed": __body5,
    // figma: Style=Filled lighter, Size=Small, State=Pressed
    "style2=filled lighter|size=sm|state=pressed": __body6,
    // figma: Style=Outline, Size=Small, State=Focus
    "style2=outline|size=sm|state=focus": __body7,
    // figma: Style=Filled darker, Size=Small, State=Focus
    "style2=filled darker|size=sm|state=focus": __body8,
    // figma: Style=Filled lighter, Size=Small, State=Focus
    "style2=filled lighter|size=sm|state=focus": __body9,
    // figma: Style=Outline, Size=Small, State=Error
    "style2=outline|size=sm|state=error": __body10,
    // figma: Style=Filled darker, Size=Small, State=Error
    "style2=filled darker|size=sm|state=error": __body11,
    // figma: Style=Filled lighter, Size=Small, State=Error
    "style2=filled lighter|size=sm|state=error": __body10,
    // figma: Style=Outline, Size=Small, State=Disabled
    "style2=outline|size=sm|state=disabled": __body12,
    // figma: Style=Filled darker, Size=Small, State=Disabled
    "style2=filled darker|size=sm|state=disabled": __body13,
    // figma: Style=Filled lighter, Size=Small, State=Disabled
    "style2=filled lighter|size=sm|state=disabled": __body13,
    // figma: Style=Outline, Size=Small, State=Read only
    "style2=outline|size=sm|state=read only": __body14,
    // figma: Style=Filled darker, Size=Small, State=Read only
    "style2=filled darker|size=sm|state=read only": __body14,
    // figma: Style=Filled lighter, Size=Small, State=Read only
    "style2=filled lighter|size=sm|state=read only": __body14,
    // figma: Style=Outline, Size=Medium (Default), State=Rest
    "style2=outline|size=medium (default)|state=rest": __body15,
    // figma: Style=Filled darker, Size=Medium (Default), State=Rest
    "style2=filled darker|size=medium (default)|state=rest": __body16,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Rest
    "style2=filled lighter|size=medium (default)|state=rest": __body17,
    // figma: Style=Outline, Size=Medium (Default), State=Hover
    "style2=outline|size=medium (default)|state=hover": __body18,
    // figma: Style=Filled darker, Size=Medium (Default), State=Hover
    "style2=filled darker|size=medium (default)|state=hover": __body19,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Hover
    "style2=filled lighter|size=medium (default)|state=hover": __body17,
    // figma: Style=Outline, Size=Medium (Default), State=Pressed
    "style2=outline|size=medium (default)|state=pressed": __body20,
    // figma: Style=Filled darker, Size=Medium (Default), State=Pressed
    "style2=filled darker|size=medium (default)|state=pressed": __body21,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Pressed
    "style2=filled lighter|size=medium (default)|state=pressed": __body22,
    // figma: Style=Outline, Size=Medium (Default), State=Focus
    "style2=outline|size=medium (default)|state=focus": __body23,
    // figma: Style=Filled darker, Size=Medium (Default), State=Focus
    "style2=filled darker|size=medium (default)|state=focus": __body24,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Focus
    "style2=filled lighter|size=medium (default)|state=focus": __body25,
    // figma: Style=Outline, Size=Medium (Default), State=Error
    "style2=outline|size=medium (default)|state=error": __body26,
    // figma: Style=Filled darker, Size=Medium (Default), State=Error
    "style2=filled darker|size=medium (default)|state=error": __body27,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Error
    "style2=filled lighter|size=medium (default)|state=error": __body28,
    // figma: Style=Outline, Size=Medium (Default), State=Disabled
    "style2=outline|size=medium (default)|state=disabled": __body29,
    // figma: Style=Filled darker, Size=Medium (Default), State=Disabled
    "style2=filled darker|size=medium (default)|state=disabled": __body30,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Disabled
    "style2=filled lighter|size=medium (default)|state=disabled": __body31,
    // figma: Style=Outline, Size=Medium (Default), State=Read only
    "style2=outline|size=medium (default)|state=read only": __body32,
    // figma: Style=Filled darker, Size=Medium (Default), State=Read only
    "style2=filled darker|size=medium (default)|state=read only": __body33,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Read only
    "style2=filled lighter|size=medium (default)|state=read only": __body32,
    // figma: Style=Outline, Size=Large, State=Rest
    "style2=outline|size=lg|state=rest": __body34,
    // figma: Style=Filled darker, Size=Large, State=Rest
    "style2=filled darker|size=lg|state=rest": __body35,
    // figma: Style=Filled lighter, Size=Large, State=Rest
    "style2=filled lighter|size=lg|state=rest": __body36,
    // figma: Style=Outline, Size=Large, State=Hover
    "style2=outline|size=lg|state=hover": __body37,
    // figma: Style=Filled darker, Size=Large, State=Hover
    "style2=filled darker|size=lg|state=hover": __body38,
    // figma: Style=Filled lighter, Size=Large, State=Hover
    "style2=filled lighter|size=lg|state=hover": __body36,
    // figma: Style=Outline, Size=Large, State=Pressed
    "style2=outline|size=lg|state=pressed": __body39,
    // figma: Style=Filled darker, Size=Large, State=Pressed
    "style2=filled darker|size=lg|state=pressed": __body40,
    // figma: Style=Filled lighter, Size=Large, State=Pressed
    "style2=filled lighter|size=lg|state=pressed": __body41,
    // figma: Style=Outline, Size=Large, State=Focus
    "style2=outline|size=lg|state=focus": __body42,
    // figma: Style=Filled darker, Size=Large, State=Focus
    "style2=filled darker|size=lg|state=focus": __body43,
    // figma: Style=Filled lighter, Size=Large, State=Focus
    "style2=filled lighter|size=lg|state=focus": __body44,
    // figma: Style=Outline, Size=Large, State=Error
    "style2=outline|size=lg|state=error": __body45,
    // figma: Style=Filled darker, Size=Large, State=Error
    "style2=filled darker|size=lg|state=error": __body46,
    // figma: Style=Filled lighter, Size=Large, State=Error
    "style2=filled lighter|size=lg|state=error": __body47,
    // figma: Style=Outline, Size=Large, State=Disabled
    "style2=outline|size=lg|state=disabled": __body48,
    // figma: Style=Filled darker, Size=Large, State=Disabled
    "style2=filled darker|size=lg|state=disabled": __body49,
    // figma: Style=Filled lighter, Size=Large, State=Disabled
    "style2=filled lighter|size=lg|state=disabled": __body49,
    // figma: Style=Underline, Size=Small, State=Rest
    "style2=underline|size=sm|state=rest": __body50,
    // figma: Style=Underline, Size=Small, State=Hover
    "style2=underline|size=sm|state=hover": __body51,
    // figma: Style=Underline, Size=Small, State=Pressed
    "style2=underline|size=sm|state=pressed": __body52,
    // figma: Style=Underline, Size=Small, State=Focus
    "style2=underline|size=sm|state=focus": __body53,
    // figma: Style=Underline, Size=Small, State=Error
    "style2=underline|size=sm|state=error": __body54,
    // figma: Style=Underline, Size=Small, State=Disabled
    "style2=underline|size=sm|state=disabled": __body55,
    // figma: Style=Underline, Size=Small, State=Read only
    "style2=underline|size=sm|state=read only": __body56,
    // figma: Style=Underline, Size=Medium (Default), State=Rest
    "style2=underline|size=medium (default)|state=rest": __body57,
    // figma: Style=Underline, Size=Medium (Default), State=Hover
    "style2=underline|size=medium (default)|state=hover": __body58,
    // figma: Style=Underline, Size=Medium (Default), State=Pressed
    "style2=underline|size=medium (default)|state=pressed": __body59,
    // figma: Style=Underline, Size=Medium (Default), State=Focus
    "style2=underline|size=medium (default)|state=focus": __body60,
    // figma: Style=Underline, Size=Medium (Default), State=Error
    "style2=underline|size=medium (default)|state=error": __body61,
    // figma: Style=Underline, Size=Medium (Default), State=Disabled
    "style2=underline|size=medium (default)|state=disabled": __body62,
    // figma: Style=Underline, Size=Medium (Default), State=Read only
    "style2=underline|size=medium (default)|state=read only": __body63,
    // figma: Style=Underline, Size=Large, State=Rest
    "style2=underline|size=lg|state=rest": __body64,
    // figma: Style=Underline, Size=Large, State=Hover
    "style2=underline|size=lg|state=hover": __body65,
    // figma: Style=Underline, Size=Large, State=Pressed
    "style2=underline|size=lg|state=pressed": __body66,
    // figma: Style=Underline, Size=Large, State=Focus
    "style2=underline|size=lg|state=focus": __body67,
    // figma: Style=Underline, Size=Large, State=Error
    "style2=underline|size=lg|state=error": __body68,
    // figma: Style=Underline, Size=Large, State=Disabled
    "style2=underline|size=lg|state=disabled": __body69,
    // figma: Style=Underline, Size=Large, State=Read only
    "style2=underline|size=lg|state=read only": __body70,
    // figma: Style=Outline, Size=Large, State=Read only
    "style2=outline|size=lg|state=read only": __body71,
    // figma: Style=Filled darker, Size=Large, State=Read only
    "style2=filled darker|size=lg|state=read only": __body72,
    // figma: Style=Filled lighter, Size=Large, State=Read only
    "style2=filled lighter|size=lg|state=read only": __body72,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Input;
