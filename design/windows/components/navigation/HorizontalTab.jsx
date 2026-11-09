import { __slateNorm } from '../internal/slateProps.jsx';
import { Calendar3 } from '../internal/Calendar3.jsx';

// figma node: 9116:18470 Horizontal Tab (80 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "selected=" + __venc(p.selected) + '|' + "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state);

export function HorizontalTab(_p = {}) {
  _p = __slateNorm(_p, {"style2":["transparent (default)","subtle","filled circular","subtle circular"],"size":["medium (default)","sm"],"state":["rest","hover","pressed","focus","disabled"]});
  const props = { ..._p, iconUnselected: _p.iconUnselected ?? true, selected: _p.selected ?? false, style2: _p.style2 ?? "transparent (default)", size: _p.size ?? "medium (default)", state: _p.state ?? "rest", iconSelected: _p.iconSelected ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-hover)",
        }} />
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-pressed)",
        }} />
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      margin: "-0.5px -1px",
      width: "fit-content",
      borderRadius: 2,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "12px 10px 8px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconSelected && (
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
                color: "var(--brand-foreground-compound-rest)",
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 3,
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 12,
            right: 12,
            top: 0,
            height: 3,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
      borderRadius: 4,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
        }} />
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }} />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-hover)",
        }} />
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-pressed)",
        }} />
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      margin: "-0.5px -1px",
      width: "fit-content",
      borderRadius: 2,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "12px 10px 8px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconUnselected && (
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
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 3,
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
      borderRadius: 4,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-hover)",
        }} />
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-pressed)",
        }} />
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      margin: "-0.5px -1px",
      width: "fit-content",
      borderRadius: 2,
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "12px 10px 8px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconSelected && (
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
                color: "var(--brand-foreground-compound-rest)",
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 3,
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 12,
            right: 12,
            top: 0,
            height: 3,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-hover)",
        }} />
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 44,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 41,
        right: 0,
        height: 3,
      }}>
        <div style={{
          position: "absolute",
          left: 12,
          right: 12,
          top: 0,
          height: 3,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-pressed)",
        }} />
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      margin: "-0.5px -1px",
      width: "fit-content",
      borderRadius: 2,
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "12px 10px 8px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconUnselected && (
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
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 3,
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-hover)",
        }} />
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-pressed)",
        }} />
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      margin: "-4px -4px",
      width: "fit-content",
      borderRadius: 2,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderRight: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderBottom: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderLeft: "3px solid var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          padding: "6px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconSelected && (
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
                color: "var(--brand-foreground-compound-rest)",
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 2,
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 8,
            right: 8,
            top: 0,
            height: 2,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 4,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
        }} />
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }} />
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-hover)",
        }} />
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-pressed)",
        }} />
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      margin: "-4px -4px",
      width: "fit-content",
      borderRadius: 2,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderRight: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderBottom: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderLeft: "3px solid var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          padding: "6px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconUnselected && (
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
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 2,
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 4,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }} />
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-hover)",
        }} />
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--brand-stroke-compound-pressed)",
        }} />
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      margin: "-4px -4px",
      width: "fit-content",
      borderRadius: 2,
      borderTop: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderRight: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderBottom: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderLeft: "3px solid var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          padding: "6px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconSelected && (
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
                color: "var(--brand-foreground-compound-rest)",
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 2,
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 8,
            right: 8,
            top: 0,
            height: 2,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-hover)",
        }} />
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        right: 0,
        height: 2,
      }}>
        <div style={{
          position: "absolute",
          left: 8,
          right: 8,
          top: 0,
          height: 2,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-stroke-1-pressed)",
        }} />
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      margin: "-4px -4px",
      width: "fit-content",
      borderRadius: 2,
      borderTop: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderRight: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderBottom: "3px solid var(--neutral-stroke-focus-2-rest)",
      borderLeft: "3px solid var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          padding: "6px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.iconUnselected && (
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
              }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
          </div>
          )}
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
            }}>{props.text1 ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 2,
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-hover)",
      borderTop: "1px solid var(--brand-stroke-compound-hover)",
      borderRight: "1px solid var(--brand-stroke-compound-hover)",
      borderBottom: "1px solid var(--brand-stroke-compound-hover)",
      borderLeft: "1px solid var(--brand-stroke-compound-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--brand-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-pressed)",
      borderTop: "1px solid var(--brand-stroke-compound-pressed)",
      borderRight: "1px solid var(--brand-stroke-compound-pressed)",
      borderBottom: "1px solid var(--brand-stroke-compound-pressed)",
      borderLeft: "1px solid var(--brand-stroke-compound-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--brand-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
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
        {props.iconSelected && (
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
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
        </div>
        )}
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
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      margin: props.style2 === "filled circular" ? "0 -1px" : undefined,
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
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
        {props.iconUnselected && (
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
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        </div>
        )}
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
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
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
        {props.iconSelected && (
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
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"filled"} />}</div>
        </div>
        )}
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
            color: "var(--neutral-foreground-on-brand-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 32,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
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
        {props.iconUnselected && (
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
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
        </div>
        )}
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
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-hover)",
      borderTop: "1px solid var(--brand-stroke-compound-hover)",
      borderRight: "1px solid var(--brand-stroke-compound-hover)",
      borderBottom: "1px solid var(--brand-stroke-compound-hover)",
      borderLeft: "1px solid var(--brand-stroke-compound-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--brand-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-pressed)",
      borderTop: "1px solid var(--brand-stroke-compound-pressed)",
      borderRight: "1px solid var(--brand-stroke-compound-pressed)",
      borderBottom: "1px solid var(--brand-stroke-compound-pressed)",
      borderLeft: "1px solid var(--brand-stroke-compound-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--brand-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      margin: "0 1px",
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        boxShadow: "0 0 0 1px var(--neutral-stroke-focus-1-rest)",
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
        alignSelf: "stretch",
      }}>
        {props.iconSelected && (
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
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
        </div>
        )}
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
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      margin: props.style2 === "filled circular" ? "0 -1px" : undefined,
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      margin: "0 -1px",
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        boxShadow: "0 0 0 1px var(--neutral-stroke-focus-1-rest)",
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
        alignSelf: "stretch",
      }}>
        {props.iconUnselected && (
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
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        </div>
        )}
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
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-disabled-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconSelected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        boxShadow: "0 0 0 1px var(--neutral-stroke-focus-1-rest)",
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
        alignSelf: "stretch",
      }}>
        {props.iconSelected && (
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
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-on-brand-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"filled"} />}</div>
        </div>
        )}
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
            color: "var(--neutral-foreground-on-brand-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-hover)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-pressed)",
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
      position: "relative",
      ...props.style,
    }}>
      {props.iconUnselected && (
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
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
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
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Text"}</span>
      </div>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-3-rest)",
      boxShadow: "0 0 0 3px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        boxShadow: "0 0 0 1px var(--neutral-stroke-focus-1-rest)",
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
        alignSelf: "stretch",
      }}>
        {props.iconUnselected && (
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
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Calendar3 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
        </div>
        )}
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
          }}>{props.text1 ?? "Text"}</span>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Selected=Yes, Style=Transparent (default), Size=Medium (default), State=Rest
    "selected=true|style2=transparent (default)|size=medium (default)|state=rest": __body0,
    // figma: Selected=Yes, Style=Transparent (default), Size=Medium (default), State=Hover
    "selected=true|style2=transparent (default)|size=medium (default)|state=hover": __body1,
    // figma: Selected=Yes, Style=Transparent (default), Size=Medium (default), State=Pressed
    "selected=true|style2=transparent (default)|size=medium (default)|state=pressed": __body2,
    // figma: Selected=Yes, Style=Transparent (default), Size=Medium (default), State=Focus
    "selected=true|style2=transparent (default)|size=medium (default)|state=focus": __body3,
    // figma: Selected=Yes, Style=Transparent (default), Size=Medium (default), State=Disabled
    "selected=true|style2=transparent (default)|size=medium (default)|state=disabled": __body4,
    // figma: Selected=No, Style=Transparent (default), Size=Medium (default), State=Rest
    "selected=false|style2=transparent (default)|size=medium (default)|state=rest": __body5,
    // figma: Selected=No, Style=Transparent (default), Size=Medium (default), State=Hover
    "selected=false|style2=transparent (default)|size=medium (default)|state=hover": __body6,
    // figma: Selected=No, Style=Transparent (default), Size=Medium (default), State=Pressed
    "selected=false|style2=transparent (default)|size=medium (default)|state=pressed": __body7,
    // figma: Selected=No, Style=Transparent (default), Size=Medium (default), State=Focus
    "selected=false|style2=transparent (default)|size=medium (default)|state=focus": __body8,
    // figma: Selected=No, Style=Transparent (default), Size=Medium (default), State=Disabled
    "selected=false|style2=transparent (default)|size=medium (default)|state=disabled": __body9,
    // figma: Selected=Yes, Style=Subtle, Size=Medium (default), State=Rest
    "selected=true|style2=subtle|size=medium (default)|state=rest": __body0,
    // figma: Selected=Yes, Style=Subtle, Size=Medium (default), State=Hover
    "selected=true|style2=subtle|size=medium (default)|state=hover": __body10,
    // figma: Selected=Yes, Style=Subtle, Size=Medium (default), State=Pressed
    "selected=true|style2=subtle|size=medium (default)|state=pressed": __body11,
    // figma: Selected=Yes, Style=Subtle, Size=Medium (default), State=Focus
    "selected=true|style2=subtle|size=medium (default)|state=focus": __body12,
    // figma: Selected=Yes, Style=Subtle, Size=Medium (default), State=Disabled
    "selected=true|style2=subtle|size=medium (default)|state=disabled": __body4,
    // figma: Selected=No, Style=Subtle, Size=Medium (default), State=Rest
    "selected=false|style2=subtle|size=medium (default)|state=rest": __body5,
    // figma: Selected=No, Style=Subtle, Size=Medium (default), State=Hover
    "selected=false|style2=subtle|size=medium (default)|state=hover": __body13,
    // figma: Selected=No, Style=Subtle, Size=Medium (default), State=Pressed
    "selected=false|style2=subtle|size=medium (default)|state=pressed": __body14,
    // figma: Selected=No, Style=Subtle, Size=Medium (default), State=Focus
    "selected=false|style2=subtle|size=medium (default)|state=focus": __body15,
    // figma: Selected=No, Style=Subtle, Size=Medium (default), State=Disabled
    "selected=false|style2=subtle|size=medium (default)|state=disabled": __body9,
    // figma: Selected=Yes, Style=Transparent (default), Size=Small, State=Rest
    "selected=true|style2=transparent (default)|size=sm|state=rest": __body16,
    // figma: Selected=Yes, Style=Transparent (default), Size=Small, State=Hover
    "selected=true|style2=transparent (default)|size=sm|state=hover": __body17,
    // figma: Selected=Yes, Style=Transparent (default), Size=Small, State=Pressed
    "selected=true|style2=transparent (default)|size=sm|state=pressed": __body18,
    // figma: Selected=Yes, Style=Transparent (default), Size=Small, State=Focus
    "selected=true|style2=transparent (default)|size=sm|state=focus": __body19,
    // figma: Selected=Yes, Style=Transparent (default), Size=Small, State=Disabled
    "selected=true|style2=transparent (default)|size=sm|state=disabled": __body20,
    // figma: Selected=No, Style=Transparent (default), Size=Small, State=Rest
    "selected=false|style2=transparent (default)|size=sm|state=rest": __body21,
    // figma: Selected=No, Style=Transparent (default), Size=Small, State=Hover
    "selected=false|style2=transparent (default)|size=sm|state=hover": __body22,
    // figma: Selected=No, Style=Transparent (default), Size=Small, State=Pressed
    "selected=false|style2=transparent (default)|size=sm|state=pressed": __body23,
    // figma: Selected=No, Style=Transparent (default), Size=Small, State=Focus
    "selected=false|style2=transparent (default)|size=sm|state=focus": __body24,
    // figma: Selected=No, Style=Transparent (default), Size=Small, State=Disabled
    "selected=false|style2=transparent (default)|size=sm|state=disabled": __body25,
    // figma: Selected=Yes, Style=Subtle, Size=Small, State=Rest
    "selected=true|style2=subtle|size=sm|state=rest": __body16,
    // figma: Selected=Yes, Style=Subtle, Size=Small, State=Hover
    "selected=true|style2=subtle|size=sm|state=hover": __body26,
    // figma: Selected=Yes, Style=Subtle, Size=Small, State=Pressed
    "selected=true|style2=subtle|size=sm|state=pressed": __body27,
    // figma: Selected=Yes, Style=Subtle, Size=Small, State=Focus
    "selected=true|style2=subtle|size=sm|state=focus": __body28,
    // figma: Selected=Yes, Style=Subtle, Size=Small, State=Disabled
    "selected=true|style2=subtle|size=sm|state=disabled": __body20,
    // figma: Selected=No, Style=Subtle, Size=Small, State=Rest
    "selected=false|style2=subtle|size=sm|state=rest": __body21,
    // figma: Selected=No, Style=Subtle, Size=Small, State=Hover
    "selected=false|style2=subtle|size=sm|state=hover": __body29,
    // figma: Selected=No, Style=Subtle, Size=Small, State=Pressed
    "selected=false|style2=subtle|size=sm|state=pressed": __body30,
    // figma: Selected=No, Style=Subtle, Size=Small, State=Focus
    "selected=false|style2=subtle|size=sm|state=focus": __body31,
    // figma: Selected=No, Style=Subtle, Size=Small, State=Disabled
    "selected=false|style2=subtle|size=sm|state=disabled": __body25,
    // figma: Selected=Yes, Style=Subtle circular, Size=Medium (default), State=Rest
    "selected=true|style2=subtle circular|size=medium (default)|state=rest": __body32,
    // figma: Selected=Yes, Style=Subtle circular, Size=Medium (default), State=Hover
    "selected=true|style2=subtle circular|size=medium (default)|state=hover": __body33,
    // figma: Selected=Yes, Style=Subtle circular, Size=Medium (default), State=Pressed
    "selected=true|style2=subtle circular|size=medium (default)|state=pressed": __body34,
    // figma: Selected=Yes, Style=Subtle circular, Size=Medium (default), State=Focus
    "selected=true|style2=subtle circular|size=medium (default)|state=focus": __body35,
    // figma: Selected=Yes, Style=Subtle circular, Size=Medium (default), State=Disabled
    "selected=true|style2=subtle circular|size=medium (default)|state=disabled": __body36,
    // figma: Selected=No, Style=Subtle circular, Size=Medium (default), State=Rest
    "selected=false|style2=subtle circular|size=medium (default)|state=rest": __body37,
    // figma: Selected=No, Style=Subtle circular, Size=Medium (default), State=Hover
    "selected=false|style2=subtle circular|size=medium (default)|state=hover": __body38,
    // figma: Selected=No, Style=Subtle circular, Size=Medium (default), State=Pressed
    "selected=false|style2=subtle circular|size=medium (default)|state=pressed": __body39,
    // figma: Selected=No, Style=Subtle circular, Size=Medium (default), State=Focus
    "selected=false|style2=subtle circular|size=medium (default)|state=focus": __body40,
    // figma: Selected=No, Style=Subtle circular, Size=Medium (default), State=Disabled
    "selected=false|style2=subtle circular|size=medium (default)|state=disabled": __body41,
    // figma: Selected=Yes, Style=Filled circular, Size=Medium (default), State=Rest
    "selected=true|style2=filled circular|size=medium (default)|state=rest": __body42,
    // figma: Selected=Yes, Style=Filled circular, Size=Medium (default), State=Hover
    "selected=true|style2=filled circular|size=medium (default)|state=hover": __body43,
    // figma: Selected=Yes, Style=Filled circular, Size=Medium (default), State=Pressed
    "selected=true|style2=filled circular|size=medium (default)|state=pressed": __body44,
    // figma: Selected=Yes, Style=Filled circular, Size=Medium (default), State=Focus
    "selected=true|style2=filled circular|size=medium (default)|state=focus": __body45,
    // figma: Selected=Yes, Style=Filled circular, Size=Medium (default), State=Disabled
    "selected=true|style2=filled circular|size=medium (default)|state=disabled": __body36,
    // figma: Selected=No, Style=Filled circular, Size=Medium (default), State=Rest
    "selected=false|style2=filled circular|size=medium (default)|state=rest": __body46,
    // figma: Selected=No, Style=Filled circular, Size=Medium (default), State=Hover
    "selected=false|style2=filled circular|size=medium (default)|state=hover": __body47,
    // figma: Selected=No, Style=Filled circular, Size=Medium (default), State=Pressed
    "selected=false|style2=filled circular|size=medium (default)|state=pressed": __body48,
    // figma: Selected=No, Style=Filled circular, Size=Medium (default), State=Focus
    "selected=false|style2=filled circular|size=medium (default)|state=focus": __body49,
    // figma: Selected=No, Style=Filled circular, Size=Medium (default), State=Disabled
    "selected=false|style2=filled circular|size=medium (default)|state=disabled": __body41,
    // figma: Selected=Yes, Style=Subtle circular, Size=Small, State=Rest
    "selected=true|style2=subtle circular|size=sm|state=rest": __body50,
    // figma: Selected=Yes, Style=Subtle circular, Size=Small, State=Hover
    "selected=true|style2=subtle circular|size=sm|state=hover": __body51,
    // figma: Selected=Yes, Style=Subtle circular, Size=Small, State=Pressed
    "selected=true|style2=subtle circular|size=sm|state=pressed": __body52,
    // figma: Selected=Yes, Style=Subtle circular, Size=Small, State=Focus
    "selected=true|style2=subtle circular|size=sm|state=focus": __body53,
    // figma: Selected=Yes, Style=Subtle circular, Size=Small, State=Disabled
    "selected=true|style2=subtle circular|size=sm|state=disabled": __body54,
    // figma: Selected=No, Style=Subtle circular, Size=Small, State=Rest
    "selected=false|style2=subtle circular|size=sm|state=rest": __body55,
    // figma: Selected=No, Style=Subtle circular, Size=Small, State=Hover
    "selected=false|style2=subtle circular|size=sm|state=hover": __body56,
    // figma: Selected=No, Style=Subtle circular, Size=Small, State=Pressed
    "selected=false|style2=subtle circular|size=sm|state=pressed": __body57,
    // figma: Selected=No, Style=Subtle circular, Size=Small, State=Focus
    "selected=false|style2=subtle circular|size=sm|state=focus": __body58,
    // figma: Selected=No, Style=Subtle circular, Size=Small, State=Disabled
    "selected=false|style2=subtle circular|size=sm|state=disabled": __body59,
    // figma: Selected=Yes, Style=Filled circular, Size=Small, State=Rest
    "selected=true|style2=filled circular|size=sm|state=rest": __body60,
    // figma: Selected=Yes, Style=Filled circular, Size=Small, State=Hover
    "selected=true|style2=filled circular|size=sm|state=hover": __body61,
    // figma: Selected=Yes, Style=Filled circular, Size=Small, State=Pressed
    "selected=true|style2=filled circular|size=sm|state=pressed": __body62,
    // figma: Selected=Yes, Style=Filled circular, Size=Small, State=Focus
    "selected=true|style2=filled circular|size=sm|state=focus": __body63,
    // figma: Selected=Yes, Style=Filled circular, Size=Small, State=Disabled
    "selected=true|style2=filled circular|size=sm|state=disabled": __body54,
    // figma: Selected=No, Style=Filled circular, Size=Small, State=Rest
    "selected=false|style2=filled circular|size=sm|state=rest": __body64,
    // figma: Selected=No, Style=Filled circular, Size=Small, State=Hover
    "selected=false|style2=filled circular|size=sm|state=hover": __body65,
    // figma: Selected=No, Style=Filled circular, Size=Small, State=Pressed
    "selected=false|style2=filled circular|size=sm|state=pressed": __body66,
    // figma: Selected=No, Style=Filled circular, Size=Small, State=Focus
    "selected=false|style2=filled circular|size=sm|state=focus": __body67,
    // figma: Selected=No, Style=Filled circular, Size=Small, State=Disabled
    "selected=false|style2=filled circular|size=sm|state=disabled": __body59,
  };
  return (__impls[__vkey(props)] ?? __body5)();
}
export default HorizontalTab;
