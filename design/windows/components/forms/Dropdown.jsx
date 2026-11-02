import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron3 } from '../internal/Chevron3.jsx';
import { ListItem } from '../navigation/ListItem.jsx';
import { Text2 } from '../internal/Text2.jsx';

// figma node: 9159:2470 Dropdown (24 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "appearance=" + __venc(p.appearance) + '|' + "size=" + __venc(p.size) + '|' + "expanded=" + __venc(p.expanded);

export function Dropdown(_p = {}) {
  _p = __slateNorm(_p, {"appearance":["outline","transparent","fill lighter","fill darker"],"size":["sm","md","lg"]});
  const props = { ..._p, appearance: _p.appearance ?? "outline", size: _p.size ?? "sm", expanded: _p.expanded ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 312,
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
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 22,
          width: 312,
          height: 2,
          borderRadius: 1,
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            flexGrow: 1,
          }}>
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 22,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          alignItems: "center",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 22,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-3-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 22,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 312,
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
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 312,
          height: 2,
          borderRadius: 1,
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-3-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 38,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-3-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 38,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
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
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 38,
          width: 312,
          height: 2,
          borderRadius: "0px 0px 4px 4px",
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 312,
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
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 38,
          width: 312,
          height: 2,
          borderRadius: 1,
          backgroundColor: "var(--brand-stroke-compound-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "var(--shadow-16)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            state={"rest"}
            type={"header"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
          <ListItem
            style={{
              position: "relative",
              height: 32,
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            checkmark={false}
            state={"rest"}
            type={"single select"}
          />
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 31,
          width: 312,
          height: 1,
          borderRadius: 4,
          backgroundColor: "var(--neutral-stroke-accessible-rest)",
        }} />
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
          }}>
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                height: 24,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 39,
          width: 312,
          height: 1,
          borderRadius: 4,
          backgroundColor: "var(--neutral-stroke-accessible-rest)",
        }} />
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          alignItems: "center",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 23,
          width: 312,
          height: 1,
          borderRadius: 4,
          backgroundColor: "var(--neutral-stroke-accessible-rest)",
        }} />
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 312,
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
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 23,
          width: 312,
          height: 1,
          borderRadius: 4,
          backgroundColor: "var(--neutral-stroke-accessible-rest)",
        }} />
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 312,
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
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 31,
          width: 312,
          height: 1,
          borderRadius: 4,
          backgroundColor: "var(--neutral-stroke-accessible-rest)",
        }} />
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 312,
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
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
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
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 39,
          width: 312,
          height: 1,
          borderRadius: 4,
          backgroundColor: "var(--neutral-stroke-accessible-rest)",
        }} />
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-3-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 32,
                flexGrow: 1,
                width: "auto",
              }}
              size={"md"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-3-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 24,
                flexGrow: 1,
                width: "auto",
              }}
              size={"sm"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"16"}
              theme={"regular"}
            />
          </div>
        </div>
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 312,
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
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-3-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
            <Text2
              style={{
                position: "relative",
                height: 34,
                flexGrow: 1,
                width: "auto",
              }}
              size={"lg"}
            />
          </div>
          <div style={{
            position: "relative",
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
            <Chevron3
              style={{
                position: "relative",
                width: 24,
                flexShrink: 0,
                alignSelf: "stretch",
                height: "auto",
                color: "var(--neutral-foreground-3-rest-3)",
              }}
              direction={"down"}
              size={"24"}
              theme={"regular"}
            />
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Appearance=Transparent, Size=Small, Expanded=True
    "appearance=transparent|size=sm|expanded=true": __body0,
    // figma: Appearance=Fill lighter, Size=Small, Expanded=True
    "appearance=fill lighter|size=sm|expanded=true": __body1,
    // figma: Appearance=Outline, Size=Small, Expanded=True
    "appearance=outline|size=sm|expanded=true": __body2,
    // figma: Appearance=Fill darker, Size=Small, Expanded=True
    "appearance=fill darker|size=sm|expanded=true": __body3,
    // figma: Appearance=Outline, Size=Medium, Expanded=True
    "appearance=outline|size=md|expanded=true": __body4,
    // figma: Appearance=Transparent, Size=Medium, Expanded=True
    "appearance=transparent|size=md|expanded=true": __body5,
    // figma: Appearance=Fill lighter, Size=Medium, Expanded=True
    "appearance=fill lighter|size=md|expanded=true": __body6,
    // figma: Appearance=Fill darker, Size=Medium, Expanded=True
    "appearance=fill darker|size=md|expanded=true": __body7,
    // figma: Appearance=Fill lighter, Size=Large, Expanded=True
    "appearance=fill lighter|size=lg|expanded=true": __body8,
    // figma: Appearance=Fill darker, Size=Large, Expanded=True
    "appearance=fill darker|size=lg|expanded=true": __body9,
    // figma: Appearance=Outline, Size=Large, Expanded=True
    "appearance=outline|size=lg|expanded=true": __body10,
    // figma: Appearance=Transparent, Size=Large, Expanded=True
    "appearance=transparent|size=lg|expanded=true": __body11,
    // figma: Appearance=Outline, Size=Medium, Expanded=False
    "appearance=outline|size=md|expanded=false": __body12,
    // figma: Appearance=Outline, Size=Large, Expanded=False
    "appearance=outline|size=lg|expanded=false": __body13,
    // figma: Appearance=Outline, Size=Small, Expanded=False
    "appearance=outline|size=sm|expanded=false": __body14,
    // figma: Appearance=Transparent, Size=Small, Expanded=False
    "appearance=transparent|size=sm|expanded=false": __body15,
    // figma: Appearance=Transparent, Size=Medium, Expanded=False
    "appearance=transparent|size=md|expanded=false": __body16,
    // figma: Appearance=Transparent, Size=Large, Expanded=False
    "appearance=transparent|size=lg|expanded=false": __body17,
    // figma: Appearance=Fill lighter, Size=Small, Expanded=False
    "appearance=fill lighter|size=sm|expanded=false": __body18,
    // figma: Appearance=Fill lighter, Size=Medium, Expanded=False
    "appearance=fill lighter|size=md|expanded=false": __body19,
    // figma: Appearance=Fill lighter, Size=Large, Expanded=False
    "appearance=fill lighter|size=lg|expanded=false": __body20,
    // figma: Appearance=Fill darker, Size=Medium, Expanded=False
    "appearance=fill darker|size=md|expanded=false": __body21,
    // figma: Appearance=Fill darker, Size=Small, Expanded=False
    "appearance=fill darker|size=sm|expanded=false": __body22,
    // figma: Appearance=Fill darker, Size=Large, Expanded=False
    "appearance=fill darker|size=lg|expanded=false": __body23,
  };
  return (__impls[__vkey(props)] ?? __body2)();
}
export default Dropdown;
