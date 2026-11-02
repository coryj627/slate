import { __slateNorm } from '../internal/slateProps.jsx';
import { Dismiss2 } from '../internal/Dismiss2.jsx';
import { Filter } from '../internal/Filter.jsx';
import { Search } from '../internal/Search.jsx';

// figma node: 9315:3026 SearchBox (36 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state);

export function SearchBox(_p = {}) {
  _p = __slateNorm(_p, {"style2":["filled darker (default)","filled lighter","outline","transparent"],"size":["sm","medium (default)","lg"],"state":["rest","focus","hover"]});
  const props = { ..._p, style2: _p.style2 ?? "filled darker (default)", size: _p.size ?? "sm", state: _p.state ?? "rest", focusActions: _p.focusActions ?? false, secondaryAction: _p.secondaryAction ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 468,
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
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 2px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <Filter size={"16"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter16pxRegular ?? <Dismiss2 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 42,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 2px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <Filter size={"16"} theme={"regular"} />}</div>
          )}
          {props.focusActions && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter16pxRegular ?? <Dismiss2 size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 42,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 2px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <Filter size={"16"} theme={"regular"} />}</div>
          )}
          {props.focusActions && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter16pxRegular ?? <Dismiss2 size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 468,
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
        padding: "0px 10px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 42,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "6px 2px 6px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter216pxRegular ?? <Filter size={"16"} theme={"regular"} />}</div>
          )}
          {props.focusActions && (
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter16pxRegular ?? <Dismiss2 size={"20"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"20"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 468,
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 2px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter212pxRegular ?? <Filter size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter12pxRegular ?? <Dismiss2 size={"12"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 2px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter212pxRegular ?? <Filter size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter12pxRegular ?? <Dismiss2 size={"12"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 2px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter212pxRegular ?? <Filter size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter12pxRegular ?? <Dismiss2 size={"12"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 468,
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "4px 2px 4px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter212pxRegular ?? <Filter size={"12"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter12pxRegular ?? <Dismiss2 size={"12"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"16"} theme={"regular"} />}</div>
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
            paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 468,
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
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 54,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 2px 8px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <Filter size={"24"} theme={"regular"} />}</div>
          )}
          {props.focusActions && (
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter24pxRegular ?? <Dismiss2 size={"24"} theme={"regular"} />}</div>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 54,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 2px 8px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <Filter size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter24pxRegular ?? <Dismiss2 size={"24"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 468,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 54,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 2px 8px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <Filter size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter24pxRegular ?? <Dismiss2 size={"24"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 468,
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
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
        {props.focusActions && (
        <div style={{
          position: "relative",
          width: 54,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "8px 2px 8px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.secondaryAction && (
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter224pxRegular ?? <Filter size={"24"} theme={"regular"} />}</div>
          )}
          <div style={{
              position: "relative",
              width: 24,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.iconAfter24pxRegular ?? <Dismiss2 size={"24"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 38,
        width: 468,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 468,
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
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
              position: "relative",
              width: 24,
              height: 24,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Search size={"24"} theme={"regular"} />}</div>
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
            }}>{props.placeholder ?? "Search"}</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 39,
        width: 468,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __impls = {
    // figma: Style=Outline, Size=Medium (Default), State=Rest
    "style2=outline|size=medium (default)|state=rest": __body0,
    // figma: Style=Filled darker (Default), Size=Medium (Default), State=Rest
    "style2=filled darker (default)|size=medium (default)|state=rest": __body1,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Rest
    "style2=filled lighter|size=medium (default)|state=rest": __body2,
    // figma: Style=Transparent, Size=Medium (Default), State=Rest
    "style2=transparent|size=medium (default)|state=rest": __body3,
    // figma: Style=Outline, Size=Medium (Default), State=Focus
    "style2=outline|size=medium (default)|state=focus": __body4,
    // figma: Style=Filled darker (Default), Size=Medium (Default), State=Focus
    "style2=filled darker (default)|size=medium (default)|state=focus": __body5,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Focus
    "style2=filled lighter|size=medium (default)|state=focus": __body6,
    // figma: Style=Transparent, Size=Medium (Default), State=Focus
    "style2=transparent|size=medium (default)|state=focus": __body7,
    // figma: Style=Outline, Size=Medium (Default), State=Hover
    "style2=outline|size=medium (default)|state=hover": __body8,
    // figma: Style=Filled darker (Default), Size=Medium (Default), State=Hover
    "style2=filled darker (default)|size=medium (default)|state=hover": __body1,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Hover
    "style2=filled lighter|size=medium (default)|state=hover": __body2,
    // figma: Style=Transparent, Size=Medium (Default), State=Hover
    "style2=transparent|size=medium (default)|state=hover": __body3,
    // figma: Style=Outline, Size=Small, State=Rest
    "style2=outline|size=sm|state=rest": __body9,
    // figma: Style=Filled darker (Default), Size=Small, State=Rest
    "style2=filled darker (default)|size=sm|state=rest": __body10,
    // figma: Style=Filled lighter, Size=Small, State=Rest
    "style2=filled lighter|size=sm|state=rest": __body11,
    // figma: Style=Transparent, Size=Small, State=Rest
    "style2=transparent|size=sm|state=rest": __body12,
    // figma: Style=Outline, Size=Small, State=Focus
    "style2=outline|size=sm|state=focus": __body13,
    // figma: Style=Filled darker (Default), Size=Small, State=Focus
    "style2=filled darker (default)|size=sm|state=focus": __body14,
    // figma: Style=Filled lighter, Size=Small, State=Focus
    "style2=filled lighter|size=sm|state=focus": __body15,
    // figma: Style=Transparent, Size=Small, State=Focus
    "style2=transparent|size=sm|state=focus": __body16,
    // figma: Style=Outline, Size=Small, State=Hover
    "style2=outline|size=sm|state=hover": __body17,
    // figma: Style=Filled darker (Default), Size=Small, State=Hover
    "style2=filled darker (default)|size=sm|state=hover": __body10,
    // figma: Style=Filled lighter, Size=Small, State=Hover
    "style2=filled lighter|size=sm|state=hover": __body11,
    // figma: Style=Transparent, Size=Small, State=Hover
    "style2=transparent|size=sm|state=hover": __body12,
    // figma: Style=Outline, Size=Large, State=Rest
    "style2=outline|size=lg|state=rest": __body18,
    // figma: Style=Filled darker (Default), Size=Large, State=Rest
    "style2=filled darker (default)|size=lg|state=rest": __body19,
    // figma: Style=Filled lighter, Size=Large, State=Rest
    "style2=filled lighter|size=lg|state=rest": __body20,
    // figma: Style=Transparent, Size=Large, State=Rest
    "style2=transparent|size=lg|state=rest": __body21,
    // figma: Style=Outline, Size=Large, State=Focus
    "style2=outline|size=lg|state=focus": __body22,
    // figma: Style=Filled darker (Default), Size=Large, State=Focus
    "style2=filled darker (default)|size=lg|state=focus": __body23,
    // figma: Style=Filled lighter, Size=Large, State=Focus
    "style2=filled lighter|size=lg|state=focus": __body24,
    // figma: Style=Transparent, Size=Large, State=Focus
    "style2=transparent|size=lg|state=focus": __body25,
    // figma: Style=Outline, Size=Large, State=Hover
    "style2=outline|size=lg|state=hover": __body26,
    // figma: Style=Filled darker (Default), Size=Large, State=Hover
    "style2=filled darker (default)|size=lg|state=hover": __body19,
    // figma: Style=Filled lighter, Size=Large, State=Hover
    "style2=filled lighter|size=lg|state=hover": __body20,
    // figma: Style=Transparent, Size=Large, State=Hover
    "style2=transparent|size=lg|state=hover": __body21,
  };
  return (__impls[__vkey(props)] ?? __body10)();
}
export default SearchBox;
