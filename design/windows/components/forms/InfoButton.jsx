import { __slateNorm } from '../internal/slateProps.jsx';
import { Info } from '../internal/Info.jsx';

// figma node: 9066:866 .Info button (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state);

export function InfoButton(_p = {}) {
  _p = __slateNorm(_p, {"size":["sm","md","lg"],"state":["rest","hover","pressed","selected (popover open)","focus"]});
  const props = { ..._p, size: _p.size ?? "sm", state: _p.state ?? "rest" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-brand-hover)",
        }}>{props.icon1 ?? <Info size={"12"} theme={"filled"} style={{ transform: "scale(1.667, 1.667)", transformOrigin: "0 0" }} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        overflow: "hidden",
        backgroundColor: "rgba(255,255,255,0)",
        flexShrink: 0,
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 5,
          top: 5,
          width: 10,
          height: 10,
          color: "var(--neutral-foreground-2-brand-selected)",
        }}>
          <path d={"M 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 Z M 4.5 5.5 L 4.5 7 C 4.5 7.276 4.724 7.5 5 7.5 C 5.276 7.5 5.5 7.276 5.5 7 L 5.5 5.5 C 5.5 5.224 5.276 5 5 5 C 4.724 5 4.5 5.224 4.5 5.5 Z M 5 2.75 C 4.586 2.75 4.25 3.086 4.25 3.5 C 4.25 3.914 4.586 4.25 5 4.25 C 5.414 4.25 5.75 3.914 5.75 3.5 C 5.75 3.086 5.414 2.75 5 2.75 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-brand-pressed)",
        }}>{props.icon1 ?? <Info size={"12"} theme={"filled"} style={{ transform: "scale(1.667, 1.667)", transformOrigin: "0 0" }} />}</div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Info size={"12"} theme={"regular"} style={{ transform: "scale(1.667, 1.667)", transformOrigin: "0 0" }} />}</div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Info size={"12"} theme={"regular"} style={{ transform: "scale(1.667, 1.667)", transformOrigin: "0 0" }} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 16,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-brand-hover)",
        }}>{props.icon1 ?? <Info size={"16"} theme={"filled"} />}</div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "column",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-brand-selected)",
        }}>{props.icon1 ?? <Info size={"16"} theme={"filled"} style={{ transform: "scale(1.500, 1.500)", transformOrigin: "0 0" }} />}</div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 16,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-brand-pressed)",
        }}>{props.icon1 ?? <Info size={"16"} theme={"filled"} />}</div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 16,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Info size={"16"} theme={"regular"} />}</div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 4px 4px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 16,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Info size={"16"} theme={"regular"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-brand-hover)",
        }}>{props.icon1 ?? <Info size={"20"} theme={"filled"} />}</div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "column",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          height: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
          color: "var(--neutral-foreground-2-brand-selected)",
        }}>{props.icon1 ?? <Info size={"20"} theme={"filled"} />}</div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-brand-pressed)",
        }}>{props.icon1 ?? <Info size={"20"} theme={"filled"} />}</div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Info size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Info size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        borderRadius: 4,
        boxShadow: "inset 0 0 0 3px var(--neutral-stroke-focus-1-rest)",
      }} />
    </div>
  );
  const __impls = {
    // figma: Size=Small, State=Hover
    "size=sm|state=hover": __body0,
    // figma: Size=Small, State=Selected (Popover open)
    "size=sm|state=selected (popover open)": __body1,
    // figma: Size=Small, State=Pressed
    "size=sm|state=pressed": __body2,
    // figma: Size=Small, State=Rest
    "size=sm|state=rest": __body3,
    // figma: Size=Small, State=Focus
    "size=sm|state=focus": __body4,
    // figma: Size=Medium, State=Hover
    "size=md|state=hover": __body5,
    // figma: Size=Medium, State=Selected (Popover open)
    "size=md|state=selected (popover open)": __body6,
    // figma: Size=Medium, State=Pressed
    "size=md|state=pressed": __body7,
    // figma: Size=Medium, State=Rest
    "size=md|state=rest": __body8,
    // figma: Size=Medium, State=Focus
    "size=md|state=focus": __body9,
    // figma: Size=Large, State=Hover
    "size=lg|state=hover": __body10,
    // figma: Size=Large, State=Selected (Popover open)
    "size=lg|state=selected (popover open)": __body11,
    // figma: Size=Large, State=Pressed
    "size=lg|state=pressed": __body12,
    // figma: Size=Large, State=Rest
    "size=lg|state=rest": __body13,
    // figma: Size=Large, State=Focus
    "size=lg|state=focus": __body14,
  };
  return (__impls[__vkey(props)] ?? __body3)();
}
export default InfoButton;
