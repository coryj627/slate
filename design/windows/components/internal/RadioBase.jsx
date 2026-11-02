import { __slateNorm } from './slateProps.jsx';

// figma node: 9121:3982 .RadioBase (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "state=" + __venc(p.state);

export function RadioBase(_p = {}) {
  _p = __slateNorm(_p, {"layout":["icon only","icon+label after","icon+label below"],"state":["default","checked","unchecked"]});
  const props = { ..._p, subtextString: _p.subtextString ?? "Subtext", subtext: _p.subtext ?? false, layout: _p.layout ?? "icon only", state: _p.state ?? "default" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-rest)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
        }}>
          <path d={"M 16 8 L 15 8 C 15 11.866 11.866 15 8 15 L 8 16 L 8 17 C 12.971 17 17 12.971 17 8 L 16 8 Z M 8 16 L 8 15 C 4.134 15 1 11.866 1 8 L 0 8 L -1 8 C -1 12.971 3.029 17 8 17 L 8 16 Z M 0 8 L 1 8 C 1 4.134 4.134 1 8 1 L 8 0 L 8 -1 C 3.029 -1 -1 3.029 -1 8 L 0 8 Z M 8 0 L 8 1 C 11.866 1 15 4.134 15 8 L 16 8 L 17 8 C 17 3.029 12.971 -1 8 -1 L 8 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <div style={{
          position: "absolute",
          left: 3,
          top: 3,
          width: 10,
          height: 10,
          borderRadius: "50%",
          backgroundColor: "var(--brand-foreground-compound-rest)",
          boxShadow: "inset 0 0 0 1px var(--brand-stroke-compound-rest)",
        }} />
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      padding: "0px 8px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
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
          width: 16,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--brand-stroke-compound-rest)",
          }}>
            <path d={"M 16 8 L 15 8 C 15 11.866 11.866 15 8 15 L 8 16 L 8 17 C 12.971 17 17 12.971 17 8 L 16 8 Z M 8 16 L 8 15 C 4.134 15 1 11.866 1 8 L 0 8 L -1 8 C -1 12.971 3.029 17 8 17 L 8 16 Z M 0 8 L 1 8 C 1 4.134 4.134 1 8 1 L 8 0 L 8 -1 C 3.029 -1 -1 3.029 -1 8 L 0 8 Z M 8 0 L 8 1 C 11.866 1 15 4.134 15 8 L 16 8 L 17 8 C 17 3.029 12.971 -1 8 -1 L 8 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "absolute",
            left: 3,
            top: 3,
            width: 10,
            height: 10,
            borderRadius: "50%",
            backgroundColor: "var(--brand-foreground-compound-rest)",
            boxShadow: "inset 0 0 0 1px var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-end",
          flexWrap: "nowrap",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
          }}>Label</span>
        </div>
        {props.subtext && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.subtextString}</span>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "0px 6px 8px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.icon1 ?? <RadioBase layout={"icon only"} state={"checked"} />}</div>
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
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
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
        width: 16,
        height: 16,
        borderRadius: "50%",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      padding: "0px 8px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <RadioBase layout={"icon only"} state={"unchecked"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          justifyContent: "flex-start",
          alignItems: "flex-end",
          flexWrap: "nowrap",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
          }}>Label</span>
        </div>
        {props.subtext && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.subtextString}</span>
        )}
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "0px 6px 8px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.icon1 ?? <RadioBase layout={"icon only"} state={"unchecked"} />}</div>
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
  );
  const __impls = {
    // figma: Layout=Icon only, State=Checked
    "layout=icon only|state=checked": __body0,
    // figma: Layout=Icon+Label after, State=Checked
    "layout=icon+label after|state=checked": __body1,
    // figma: Layout=Icon+Label below, State=Default
    "layout=icon+label below|state=default": __body2,
    // figma: Layout=Icon only, State=Unchecked
    "layout=icon only|state=unchecked": __body3,
    // figma: Layout=Icon+Label after, State=Unchecked
    "layout=icon+label after|state=unchecked": __body4,
    // figma: Layout=Icon+Label below, State=Unchecked
    "layout=icon+label below|state=unchecked": __body5,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default RadioBase;
