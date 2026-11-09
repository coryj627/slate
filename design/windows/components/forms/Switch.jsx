import { __slateNorm } from '../internal/slateProps.jsx';
import { SwitchBase } from '../internal/SwitchBase.jsx';

// figma node: 9116:20025 Switch (40 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "checked=" + __venc(p.checked) + '|' + "state=" + __venc(p.state);

export function Switch(_p = {}) {
  _p = __slateNorm(_p, {"layout":["switch","switch+label after","switch+label before","switch+label above"],"state":["rest","hover","pressed","focus","disabled"]});
  const props = { ..._p, layout: _p.layout ?? "switch", checked: _p.checked ?? true, state: _p.state ?? "rest" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            boxSizing: "border-box",
          width: 40,
            height: 20,
            borderRadius: 10,
            borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
            borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
          }} />
          <div style={{
            position: "absolute",
            left: 3,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-stroke-accessible-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            boxSizing: "border-box",
          width: 40,
            height: 20,
            borderRadius: 10,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
          }} />
          <div style={{
            position: "absolute",
            left: 3,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-stroke-accessible-hover)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            boxSizing: "border-box",
          width: 40,
            height: 20,
            borderRadius: 10,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
          }} />
          <div style={{
            position: "absolute",
            left: 3,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-stroke-accessible-pressed)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        display: "flex",
        flexDirection: "column",
        gap: 10,
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            boxSizing: "border-box",
          width: 40,
            height: 20,
            borderRadius: 10,
            borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
            borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
          }} />
          <div style={{
            position: "absolute",
            left: 3,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-stroke-accessible-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            boxSizing: "border-box",
          width: 40,
            height: 20,
            borderRadius: 10,
            borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
            borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
            borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
            borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
          }} />
          <div style={{
            position: "absolute",
            left: 3,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-foreground-disabled-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <SwitchBase
            style={{ position: "relative", flexShrink: 0 }}
            layout={"unchecked"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 4px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <SwitchBase
            style={{ position: "relative", flexShrink: 0 }}
            layout={"unchecked"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 4px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <SwitchBase
            style={{ position: "relative", flexShrink: 0 }}
            layout={"unchecked"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 4px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 4px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <SwitchBase
            style={{ position: "relative", flexShrink: 0 }}
            layout={"unchecked"}
          />
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 4px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <SwitchBase
            style={{ position: "relative", flexShrink: 0 }}
            layout={"unchecked"}
          />
        </div>
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 4px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <SwitchBase
            style={{ position: "relative", flexShrink: 0 }}
            layout={"unchecked"}
          />
        </div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 60,
          display: "flex",
          flexDirection: "column",
          padding: "4px 0px 4px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <SwitchBase
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            layout={"unchecked"}
          />
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 60,
          display: "flex",
          flexDirection: "column",
          padding: "4px 0px 4px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <SwitchBase
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            layout={"unchecked"}
          />
        </div>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 60,
          display: "flex",
          flexDirection: "column",
          padding: "4px 0px 4px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <SwitchBase
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            layout={"unchecked"}
          />
        </div>
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderRadius: 10,
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 40,
            height: 20,
            borderRadius: 10,
            backgroundColor: "var(--brand-background-compound-rest)",
            boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
          }} />
          <div style={{
            position: "absolute",
            left: 23,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-foreground-inverted-1-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderRadius: 10,
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 40,
            height: 20,
            borderRadius: 10,
            backgroundColor: "var(--brand-background-compound-hover)",
          }} />
          <div style={{
            position: "absolute",
            left: 23,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-foreground-inverted-1-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderRadius: 10,
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 40,
            height: 20,
            borderRadius: 10,
            backgroundColor: "var(--brand-background-compound-pressed)",
          }} />
          <div style={{
            position: "absolute",
            left: 23,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-foreground-inverted-1-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        display: "flex",
        flexDirection: "column",
        gap: 10,
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderRadius: 10,
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 40,
            height: 20,
            borderRadius: 10,
            backgroundColor: "var(--brand-background-compound-rest)",
            boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
          }} />
          <div style={{
            position: "absolute",
            left: 23,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-foreground-inverted-1-rest)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "8px 8px 8px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 40,
        height: 20,
          overflow: "hidden",
          borderRadius: 10,
          borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            boxSizing: "border-box",
          width: 40,
            height: 20,
            borderRadius: 10,
            backgroundColor: "var(--neutral-background-disabled-rest)",
            borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
            borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
            borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
            borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
          }} />
          <div style={{
            position: "absolute",
            left: 23,
            top: 3,
            width: 14,
            height: 14,
            borderRadius: "50%",
            backgroundColor: "var(--neutral-foreground-disabled-rest)",
            boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
          }} />
        </div>
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 4px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 4px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 4px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 4px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 4px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 4px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 60,
          display: "flex",
          flexDirection: "column",
          padding: "4px 0px 4px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <div style={{
            position: "relative",
            height: 36,
            display: "flex",
            flexDirection: "column",
            padding: "4px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 40,
        height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      margin: "-2px -2px",
      width: "fit-content",
      borderRadius: 4,
      boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
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
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 60,
          display: "flex",
          flexDirection: "column",
          padding: "4px 0px 4px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <div style={{
            position: "relative",
            height: 36,
            display: "flex",
            flexDirection: "column",
            padding: "4px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 40,
        height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 60,
          display: "flex",
          flexDirection: "column",
          padding: "4px 0px 4px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "0px 8px 0px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
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
              }}>{props.labelText ?? "Label"}</span>
            </div>
          </div>
          <div style={{
            position: "relative",
            height: 36,
            display: "flex",
            flexDirection: "column",
            padding: "4px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 40,
        height: 20,
              overflow: "hidden",
              borderRadius: 10,
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                backgroundColor: "var(--brand-background-compound-rest)",
                boxShadow: "inset 0 0 0 1px var(--slate-switch-track-border, transparent)",
              }} />
              <div style={{
                position: "absolute",
                left: 23,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--colors-neutral-white)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Layout=Switch, Checked=False, State=Rest
    "layout=switch|checked=false|state=rest": __body0,
    // figma: Layout=Switch, Checked=False, State=Hover
    "layout=switch|checked=false|state=hover": __body1,
    // figma: Layout=Switch, Checked=False, State=Pressed
    "layout=switch|checked=false|state=pressed": __body2,
    // figma: Layout=Switch, Checked=False, State=Focus
    "layout=switch|checked=false|state=focus": __body3,
    // figma: Layout=Switch, Checked=False, State=Disabled
    "layout=switch|checked=false|state=disabled": __body4,
    // figma: Layout=Switch+Label after, Checked=False, State=Rest
    "layout=switch+label after|checked=false|state=rest": __body5,
    // figma: Layout=Switch+Label after, Checked=False, State=Hover
    "layout=switch+label after|checked=false|state=hover": __body5,
    // figma: Layout=Switch+Label after, Checked=False, State=Pressed
    "layout=switch+label after|checked=false|state=pressed": __body5,
    // figma: Layout=Switch+Label after, Checked=False, State=Focus
    "layout=switch+label after|checked=false|state=focus": __body6,
    // figma: Layout=Switch+Label after, Checked=False, State=Disabled
    "layout=switch+label after|checked=false|state=disabled": __body7,
    // figma: Layout=Switch+Label before, Checked=False, State=Rest
    "layout=switch+label before|checked=false|state=rest": __body8,
    // figma: Layout=Switch+Label before, Checked=False, State=Hover
    "layout=switch+label before|checked=false|state=hover": __body8,
    // figma: Layout=Switch+Label before, Checked=False, State=Pressed
    "layout=switch+label before|checked=false|state=pressed": __body8,
    // figma: Layout=Switch+Label before, Checked=False, State=Focus
    "layout=switch+label before|checked=false|state=focus": __body9,
    // figma: Layout=Switch+Label before, Checked=False, State=Disabled
    "layout=switch+label before|checked=false|state=disabled": __body10,
    // figma: Layout=Switch+Label above, Checked=False, State=Rest
    "layout=switch+label above|checked=false|state=rest": __body11,
    // figma: Layout=Switch+Label above, Checked=False, State=Hover
    "layout=switch+label above|checked=false|state=hover": __body11,
    // figma: Layout=Switch+Label above, Checked=False, State=Pressed
    "layout=switch+label above|checked=false|state=pressed": __body11,
    // figma: Layout=Switch+Label above, Checked=False, State=Focus
    "layout=switch+label above|checked=false|state=focus": __body12,
    // figma: Layout=Switch+Label above, Checked=False, State=Disabled
    "layout=switch+label above|checked=false|state=disabled": __body13,
    // figma: Layout=Switch, Checked=True, State=Rest
    "layout=switch|checked=true|state=rest": __body14,
    // figma: Layout=Switch, Checked=True, State=Hover
    "layout=switch|checked=true|state=hover": __body15,
    // figma: Layout=Switch, Checked=True, State=Pressed
    "layout=switch|checked=true|state=pressed": __body16,
    // figma: Layout=Switch, Checked=True, State=Focus
    "layout=switch|checked=true|state=focus": __body17,
    // figma: Layout=Switch, Checked=True, State=Disabled
    "layout=switch|checked=true|state=disabled": __body18,
    // figma: Layout=Switch+Label after, Checked=True, State=Rest
    "layout=switch+label after|checked=true|state=rest": __body19,
    // figma: Layout=Switch+Label after, Checked=True, State=Hover
    "layout=switch+label after|checked=true|state=hover": __body19,
    // figma: Layout=Switch+Label after, Checked=True, State=Pressed
    "layout=switch+label after|checked=true|state=pressed": __body19,
    // figma: Layout=Switch+Label after, Checked=True, State=Focus
    "layout=switch+label after|checked=true|state=focus": __body20,
    // figma: Layout=Switch+Label after, Checked=True, State=Disabled
    "layout=switch+label after|checked=true|state=disabled": __body21,
    // figma: Layout=Switch+Label before, Checked=True, State=Rest
    "layout=switch+label before|checked=true|state=rest": __body22,
    // figma: Layout=Switch+Label before, Checked=True, State=Hover
    "layout=switch+label before|checked=true|state=hover": __body22,
    // figma: Layout=Switch+Label before, Checked=True, State=Pressed
    "layout=switch+label before|checked=true|state=pressed": __body22,
    // figma: Layout=Switch+Label before, Checked=True, State=Focus
    "layout=switch+label before|checked=true|state=focus": __body23,
    // figma: Layout=Switch+Label before, Checked=True, State=Disabled
    "layout=switch+label before|checked=true|state=disabled": __body24,
    // figma: Layout=Switch+Label above, Checked=True, State=Rest
    "layout=switch+label above|checked=true|state=rest": __body25,
    // figma: Layout=Switch+Label above, Checked=True, State=Hover
    "layout=switch+label above|checked=true|state=hover": __body25,
    // figma: Layout=Switch+Label above, Checked=True, State=Pressed
    "layout=switch+label above|checked=true|state=pressed": __body25,
    // figma: Layout=Switch+Label above, Checked=True, State=Focus
    "layout=switch+label above|checked=true|state=focus": __body26,
    // figma: Layout=Switch+Label above, Checked=True, State=Disabled
    "layout=switch+label above|checked=true|state=disabled": __body27,
  };
  return (__impls[__vkey(props)] ?? __body14)();
}
export default Switch;
