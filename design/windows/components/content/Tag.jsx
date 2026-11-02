import { __slateNorm } from '../internal/slateProps.jsx';
import { Avatar } from './Avatar.jsx';
import { Calendar } from '../internal/Calendar.jsx';

// figma node: 9000:3581 Tag (120 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "radius=" + __venc(p.radius) + '|' + "state=" + __venc(p.state) + '|' + "secondaryText=" + __venc(p.secondaryText);

export function Tag(_p = {}) {
  _p = __slateNorm(_p, {"style2":["filled","outline","brand"],"size":["md","sm","extra small"],"radius":["rounded square","circular"],"state":["rest","hover (dismiss only)","pressed (dismiss only)","disabled","focus"]});
  const props = { ..._p, avatar: _p.avatar ?? true, primaryString: _p.primaryString ?? "Primary text", secondaryString: _p.secondaryString ?? "Secondary", dismiss: _p.dismiss ?? true, iconBefore: _p.iconBefore ?? true, style2: _p.style2 ?? "filled", size: _p.size ?? "md", radius: _p.radius ?? "rounded square", state: _p.state ?? "rest", secondaryText: _p.secondaryText ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.icon2 ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.icon2 ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-pressed)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--brand-foreground-2-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-hover)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 8,
              height: 8,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
              position: "absolute",
              left: 2.5,
              top: 2.5,
              width: 11,
              height: 11,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.secondaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 8,
              height: 8,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
              position: "absolute",
              left: 2.5,
              top: 2.5,
              width: 11,
              height: 11,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--brand-foreground-1-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--brand-foreground-2-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--brand-foreground-1-rest-2)",
              flexShrink: 0,
            }}>{props.secondaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--brand-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-2-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"brand"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--brand-foreground-2-rest-2)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.icon2 ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.icon2 ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest-2)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 8,
              height: 8,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
              position: "absolute",
              left: 2.5,
              top: 2.5,
              width: 11,
              height: 11,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.secondaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 8,
              height: 8,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
              position: "absolute",
              left: 2.5,
              top: 2.5,
              width: 11,
              height: 11,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.secondaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 2,
            width: 8,
            height: 8,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 2,
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
            position: "absolute",
            left: 2.5,
            top: 2.5,
            width: 11,
            height: 11,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
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
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            whiteSpace: "nowrap",
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.secondaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 28,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 4,
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest)",
          }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest-4)",
            flexShrink: 0,
          }}>{props.primaryString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--neutral-background-1-rest)",
          }}>
            <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 8,
              height: 8,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
              position: "absolute",
              left: 2.5,
              top: 2.5,
              width: 11,
              height: 11,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.secondaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body73 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body74 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 0px 1px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"16"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance12px ?? <Calendar direction={"ltr"} size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 8,
              height: 8,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body75 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"20"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 2,
          padding: "0px 6px 0px 6px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance16px ?? <Calendar direction={"ltr"} size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
              position: "absolute",
              left: 2.5,
              top: 2.5,
              width: 11,
              height: 11,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body76 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
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
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.secondaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body77 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-3-rest-2)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 0px 2px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest-2)",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.avatar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}>{props.icon1 ?? <Avatar layout={"initials"} size={"28"} color={"neutral"} />}</div>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconBefore && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-background-1-rest)",
            }}>{props.iconInstance20px ?? <Calendar direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-4)",
              flexShrink: 0,
            }}>{props.primaryString}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            borderRadius: 4,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-background-1-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Style=Brand, Size=Extra Small, Radius=Circular, State=Disabled, Secondary text=False
    "style2=brand|size=extra small|radius=circular|state=disabled|secondaryText=false": __body0,
    // figma: Style=Brand, Size=Small, Radius=Circular, State=Disabled, Secondary text=False
    "style2=brand|size=sm|radius=circular|state=disabled|secondaryText=false": __body1,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Disabled, Secondary text=True
    "style2=brand|size=md|radius=circular|state=disabled|secondaryText=true": __body2,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Disabled, Secondary text=False
    "style2=brand|size=md|radius=circular|state=disabled|secondaryText=false": __body3,
    // figma: Style=Brand, Size=Extra Small, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=brand|size=extra small|radius=rounded square|state=disabled|secondaryText=false": __body4,
    // figma: Style=Brand, Size=Small, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=brand|size=sm|radius=rounded square|state=disabled|secondaryText=false": __body5,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Disabled, Secondary text=True
    "style2=brand|size=md|radius=rounded square|state=disabled|secondaryText=true": __body6,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=brand|size=md|radius=rounded square|state=disabled|secondaryText=false": __body7,
    // figma: Style=Brand, Size=Extra Small, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=brand|size=extra small|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body8,
    // figma: Style=Brand, Size=Small, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=brand|size=sm|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body9,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Pressed (Dismiss only), Secondary text=True
    "style2=brand|size=md|radius=circular|state=pressed (dismiss only)|secondaryText=true": __body10,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=brand|size=md|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body11,
    // figma: Style=Brand, Size=Extra Small, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=brand|size=extra small|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body12,
    // figma: Style=Brand, Size=Small, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=brand|size=sm|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body13,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=True
    "style2=brand|size=md|radius=rounded square|state=pressed (dismiss only)|secondaryText=true": __body14,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=brand|size=md|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body15,
    // figma: Style=Brand, Size=Extra Small, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=brand|size=extra small|radius=circular|state=hover (dismiss only)|secondaryText=false": __body16,
    // figma: Style=Brand, Size=Small, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=brand|size=sm|radius=circular|state=hover (dismiss only)|secondaryText=false": __body17,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Hover (Dismiss only), Secondary text=True
    "style2=brand|size=md|radius=circular|state=hover (dismiss only)|secondaryText=true": __body18,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=brand|size=md|radius=circular|state=hover (dismiss only)|secondaryText=false": __body19,
    // figma: Style=Brand, Size=Extra Small, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=brand|size=extra small|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body20,
    // figma: Style=Brand, Size=Small, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=brand|size=sm|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body21,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=True
    "style2=brand|size=md|radius=rounded square|state=hover (dismiss only)|secondaryText=true": __body22,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=brand|size=md|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body15,
    // figma: Style=Brand, Size=Extra Small, Radius=Circular, State=Rest, Secondary text=False
    "style2=brand|size=extra small|radius=circular|state=rest|secondaryText=false": __body23,
    // figma: Style=Brand, Size=Extra Small, Radius=Circular, State=Focus, Secondary text=False
    "style2=brand|size=extra small|radius=circular|state=focus|secondaryText=false": __body24,
    // figma: Style=Brand, Size=Small, Radius=Circular, State=Rest, Secondary text=False
    "style2=brand|size=sm|radius=circular|state=rest|secondaryText=false": __body25,
    // figma: Style=Brand, Size=Small, Radius=Circular, State=Focus, Secondary text=False
    "style2=brand|size=sm|radius=circular|state=focus|secondaryText=false": __body26,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Rest, Secondary text=True
    "style2=brand|size=md|radius=circular|state=rest|secondaryText=true": __body27,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Focus, Secondary text=True
    "style2=brand|size=md|radius=circular|state=focus|secondaryText=true": __body28,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Rest, Secondary text=False
    "style2=brand|size=md|radius=circular|state=rest|secondaryText=false": __body29,
    // figma: Style=Brand, Size=Medium, Radius=Circular, State=Focus, Secondary text=False
    "style2=brand|size=md|radius=circular|state=focus|secondaryText=false": __body30,
    // figma: Style=Brand, Size=Extra Small, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=brand|size=extra small|radius=rounded square|state=rest|secondaryText=false": __body31,
    // figma: Style=Brand, Size=Extra Small, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=brand|size=extra small|radius=rounded square|state=focus|secondaryText=false": __body32,
    // figma: Style=Brand, Size=Small, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=brand|size=sm|radius=rounded square|state=rest|secondaryText=false": __body33,
    // figma: Style=Brand, Size=Small, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=brand|size=sm|radius=rounded square|state=focus|secondaryText=false": __body34,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Rest, Secondary text=True
    "style2=brand|size=md|radius=rounded square|state=rest|secondaryText=true": __body35,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Focus, Secondary text=True
    "style2=brand|size=md|radius=rounded square|state=focus|secondaryText=true": __body36,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=brand|size=md|radius=rounded square|state=rest|secondaryText=false": __body15,
    // figma: Style=Brand, Size=Medium, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=brand|size=md|radius=rounded square|state=focus|secondaryText=false": __body37,
    // figma: Style=Outline, Size=Extra Small, Radius=Circular, State=Disabled, Secondary text=False
    "style2=outline|size=extra small|radius=circular|state=disabled|secondaryText=false": __body38,
    // figma: Style=Outline, Size=Small, Radius=Circular, State=Disabled, Secondary text=False
    "style2=outline|size=sm|radius=circular|state=disabled|secondaryText=false": __body39,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Disabled, Secondary text=True
    "style2=outline|size=md|radius=circular|state=disabled|secondaryText=true": __body40,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Disabled, Secondary text=False
    "style2=outline|size=md|radius=circular|state=disabled|secondaryText=false": __body41,
    // figma: Style=Outline, Size=Extra Small, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=outline|size=extra small|radius=rounded square|state=disabled|secondaryText=false": __body42,
    // figma: Style=Outline, Size=Small, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=outline|size=sm|radius=rounded square|state=disabled|secondaryText=false": __body43,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Disabled, Secondary text=True
    "style2=outline|size=md|radius=rounded square|state=disabled|secondaryText=true": __body44,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=outline|size=md|radius=rounded square|state=disabled|secondaryText=false": __body45,
    // figma: Style=Outline, Size=Extra Small, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=outline|size=extra small|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body46,
    // figma: Style=Outline, Size=Small, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=outline|size=sm|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body47,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Pressed (Dismiss only), Secondary text=True
    "style2=outline|size=md|radius=circular|state=pressed (dismiss only)|secondaryText=true": __body48,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=outline|size=md|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body49,
    // figma: Style=Outline, Size=Extra Small, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=outline|size=extra small|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body50,
    // figma: Style=Outline, Size=Small, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=outline|size=sm|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body51,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=True
    "style2=outline|size=md|radius=rounded square|state=pressed (dismiss only)|secondaryText=true": __body52,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=outline|size=md|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body53,
    // figma: Style=Outline, Size=Extra Small, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=outline|size=extra small|radius=circular|state=hover (dismiss only)|secondaryText=false": __body46,
    // figma: Style=Outline, Size=Small, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=outline|size=sm|radius=circular|state=hover (dismiss only)|secondaryText=false": __body47,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Hover (Dismiss only), Secondary text=True
    "style2=outline|size=md|radius=circular|state=hover (dismiss only)|secondaryText=true": __body48,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=outline|size=md|radius=circular|state=hover (dismiss only)|secondaryText=false": __body49,
    // figma: Style=Outline, Size=Extra Small, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=outline|size=extra small|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body50,
    // figma: Style=Outline, Size=Small, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=outline|size=sm|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body51,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=True
    "style2=outline|size=md|radius=rounded square|state=hover (dismiss only)|secondaryText=true": __body52,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=outline|size=md|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body53,
    // figma: Style=Outline, Size=Extra Small, Radius=Circular, State=Rest, Secondary text=False
    "style2=outline|size=extra small|radius=circular|state=rest|secondaryText=false": __body46,
    // figma: Style=Outline, Size=Extra Small, Radius=Circular, State=Focus, Secondary text=False
    "style2=outline|size=extra small|radius=circular|state=focus|secondaryText=false": __body54,
    // figma: Style=Outline, Size=Small, Radius=Circular, State=Rest, Secondary text=False
    "style2=outline|size=sm|radius=circular|state=rest|secondaryText=false": __body47,
    // figma: Style=Outline, Size=Small, Radius=Circular, State=Focus, Secondary text=False
    "style2=outline|size=sm|radius=circular|state=focus|secondaryText=false": __body55,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Rest, Secondary text=True
    "style2=outline|size=md|radius=circular|state=rest|secondaryText=true": __body48,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Focus, Secondary text=True
    "style2=outline|size=md|radius=circular|state=focus|secondaryText=true": __body56,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Rest, Secondary text=False
    "style2=outline|size=md|radius=circular|state=rest|secondaryText=false": __body49,
    // figma: Style=Outline, Size=Medium, Radius=Circular, State=Focus, Secondary text=False
    "style2=outline|size=md|radius=circular|state=focus|secondaryText=false": __body57,
    // figma: Style=Outline, Size=Extra Small, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=outline|size=extra small|radius=rounded square|state=rest|secondaryText=false": __body50,
    // figma: Style=Outline, Size=Extra Small, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=outline|size=extra small|radius=rounded square|state=focus|secondaryText=false": __body58,
    // figma: Style=Outline, Size=Small, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=outline|size=sm|radius=rounded square|state=rest|secondaryText=false": __body51,
    // figma: Style=Outline, Size=Small, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=outline|size=sm|radius=rounded square|state=focus|secondaryText=false": __body59,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Rest, Secondary text=True
    "style2=outline|size=md|radius=rounded square|state=rest|secondaryText=true": __body52,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Focus, Secondary text=True
    "style2=outline|size=md|radius=rounded square|state=focus|secondaryText=true": __body60,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=outline|size=md|radius=rounded square|state=rest|secondaryText=false": __body53,
    // figma: Style=Outline, Size=Medium, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=outline|size=md|radius=rounded square|state=focus|secondaryText=false": __body61,
    // figma: Style=Filled, Size=Extra Small, Radius=Circular, State=Disabled, Secondary text=False
    "style2=filled|size=extra small|radius=circular|state=disabled|secondaryText=false": __body0,
    // figma: Style=Filled, Size=Small, Radius=Circular, State=Disabled, Secondary text=False
    "style2=filled|size=sm|radius=circular|state=disabled|secondaryText=false": __body1,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Disabled, Secondary text=True
    "style2=filled|size=md|radius=circular|state=disabled|secondaryText=true": __body2,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Disabled, Secondary text=False
    "style2=filled|size=md|radius=circular|state=disabled|secondaryText=false": __body3,
    // figma: Style=Filled, Size=Extra Small, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=filled|size=extra small|radius=rounded square|state=disabled|secondaryText=false": __body4,
    // figma: Style=Filled, Size=Small, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=filled|size=sm|radius=rounded square|state=disabled|secondaryText=false": __body5,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Disabled, Secondary text=True
    "style2=filled|size=md|radius=rounded square|state=disabled|secondaryText=true": __body6,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Disabled, Secondary text=False
    "style2=filled|size=md|radius=rounded square|state=disabled|secondaryText=false": __body7,
    // figma: Style=Filled, Size=Extra Small, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=filled|size=extra small|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body62,
    // figma: Style=Filled, Size=Small, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=filled|size=sm|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body63,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Pressed (Dismiss only), Secondary text=True
    "style2=filled|size=md|radius=circular|state=pressed (dismiss only)|secondaryText=true": __body64,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Pressed (Dismiss only), Secondary text=False
    "style2=filled|size=md|radius=circular|state=pressed (dismiss only)|secondaryText=false": __body65,
    // figma: Style=Filled, Size=Extra Small, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=filled|size=extra small|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body66,
    // figma: Style=Filled, Size=Small, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=filled|size=sm|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body67,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=True
    "style2=filled|size=md|radius=rounded square|state=pressed (dismiss only)|secondaryText=true": __body68,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Pressed (Dismiss only), Secondary text=False
    "style2=filled|size=md|radius=rounded square|state=pressed (dismiss only)|secondaryText=false": __body69,
    // figma: Style=Filled, Size=Extra Small, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=filled|size=extra small|radius=circular|state=hover (dismiss only)|secondaryText=false": __body62,
    // figma: Style=Filled, Size=Small, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=filled|size=sm|radius=circular|state=hover (dismiss only)|secondaryText=false": __body63,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Hover (Dismiss only), Secondary text=True
    "style2=filled|size=md|radius=circular|state=hover (dismiss only)|secondaryText=true": __body64,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Hover (Dismiss only), Secondary text=False
    "style2=filled|size=md|radius=circular|state=hover (dismiss only)|secondaryText=false": __body65,
    // figma: Style=Filled, Size=Extra Small, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=filled|size=extra small|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body66,
    // figma: Style=Filled, Size=Small, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=filled|size=sm|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body67,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=True
    "style2=filled|size=md|radius=rounded square|state=hover (dismiss only)|secondaryText=true": __body68,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Hover (Dismiss only), Secondary text=False
    "style2=filled|size=md|radius=rounded square|state=hover (dismiss only)|secondaryText=false": __body69,
    // figma: Style=Filled, Size=Extra Small, Radius=Circular, State=Rest, Secondary text=False
    "style2=filled|size=extra small|radius=circular|state=rest|secondaryText=false": __body62,
    // figma: Style=Filled, Size=Extra Small, Radius=Circular, State=Focus, Secondary text=False
    "style2=filled|size=extra small|radius=circular|state=focus|secondaryText=false": __body70,
    // figma: Style=Filled, Size=Small, Radius=Circular, State=Rest, Secondary text=False
    "style2=filled|size=sm|radius=circular|state=rest|secondaryText=false": __body63,
    // figma: Style=Filled, Size=Small, Radius=Circular, State=Focus, Secondary text=False
    "style2=filled|size=sm|radius=circular|state=focus|secondaryText=false": __body71,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Rest, Secondary text=True
    "style2=filled|size=md|radius=circular|state=rest|secondaryText=true": __body64,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Focus, Secondary text=True
    "style2=filled|size=md|radius=circular|state=focus|secondaryText=true": __body72,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Rest, Secondary text=False
    "style2=filled|size=md|radius=circular|state=rest|secondaryText=false": __body65,
    // figma: Style=Filled, Size=Medium, Radius=Circular, State=Focus, Secondary text=False
    "style2=filled|size=md|radius=circular|state=focus|secondaryText=false": __body73,
    // figma: Style=Filled, Size=Extra Small, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=filled|size=extra small|radius=rounded square|state=rest|secondaryText=false": __body66,
    // figma: Style=Filled, Size=Extra Small, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=filled|size=extra small|radius=rounded square|state=focus|secondaryText=false": __body74,
    // figma: Style=Filled, Size=Small, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=filled|size=sm|radius=rounded square|state=rest|secondaryText=false": __body67,
    // figma: Style=Filled, Size=Small, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=filled|size=sm|radius=rounded square|state=focus|secondaryText=false": __body75,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Rest, Secondary text=True
    "style2=filled|size=md|radius=rounded square|state=rest|secondaryText=true": __body68,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Focus, Secondary text=True
    "style2=filled|size=md|radius=rounded square|state=focus|secondaryText=true": __body76,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Rest, Secondary text=False
    "style2=filled|size=md|radius=rounded square|state=rest|secondaryText=false": __body69,
    // figma: Style=Filled, Size=Medium, Radius=Rounded square, State=Focus, Secondary text=False
    "style2=filled|size=md|radius=rounded square|state=focus|secondaryText=false": __body77,
  };
  return (__impls[__vkey(props)] ?? __body69)();
}
export default Tag;
