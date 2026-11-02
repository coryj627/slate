import { __slateNorm } from './slateProps.jsx';

// figma node: 9116:19992 .SwitchBase (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout);

export function SwitchBase(_p = {}) {
  _p = __slateNorm(_p, {"layout":["switch","switch+vlabel","switch+hlabel","unchecked+hlabel","unchecked","unchecked+vlabel"]});
  const props = { ..._p, layout: _p.layout ?? "switch" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
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
      position: "relative",
      ...props.style,
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
            }}>Label</span>
          </div>
        </div>
        <div style={{ position: "relative", flexShrink: 0 }}>{props.icon2 ?? <SwitchBase layout={"unchecked"} />}</div>
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
            }}>Label</span>
          </div>
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
            }}>Label</span>
          </div>
        </div>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.icon2 ?? <SwitchBase layout={"unchecked"} />}</div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
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
      position: "relative",
      ...props.style,
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
            }}>Label</span>
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
            }}>Label</span>
          </div>
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
            }}>Label</span>
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
  );
  const __impls = {
    // figma: Layout=Unchecked
    "layout=unchecked": __body0,
    // figma: Layout=Unchecked+Vlabel
    "layout=unchecked+vlabel": __body1,
    // figma: Layout=Unchecked+Hlabel
    "layout=unchecked+hlabel": __body2,
    // figma: Layout=Switch
    "layout=switch": __body3,
    // figma: Layout=Switch+VLabel
    "layout=switch+vlabel": __body4,
    // figma: Layout=Switch+Hlabel
    "layout=switch+hlabel": __body5,
  };
  return (__impls[__vkey(props)] ?? __body3)();
}
export default SwitchBase;
