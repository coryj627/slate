import { __slateNorm } from './slateProps.jsx';
import { Chevron2 } from './Chevron2.jsx';

// figma node: 9116:28330 .Spin button stepper (8 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state);

export function SpinButtonStepper(_p = {}) {
  _p = __slateNorm(_p, {"style2":["default","darker"],"state":["rest","hover","pressed","disabled"]});
  const props = { ..._p, style2: _p.style2 ?? "default", state: _p.state ?? "rest" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-disabled-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      backgroundColor: "var(--neutral-background-3-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      backgroundColor: "var(--neutral-background-3-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __impls = {
    // figma: Style=Default, State=Rest
    "style2=default|state=rest": __body0,
    // figma: Style=Default, State=Hover
    "style2=default|state=hover": __body1,
    // figma: Style=Default, State=Pressed
    "style2=default|state=pressed": __body2,
    // figma: Style=Default, State=Disabled
    "style2=default|state=disabled": __body3,
    // figma: Style=Darker, State=Rest
    "style2=darker|state=rest": __body0,
    // figma: Style=Darker, State=Hover
    "style2=darker|state=hover": __body4,
    // figma: Style=Darker, State=Pressed
    "style2=darker|state=pressed": __body5,
    // figma: Style=Darker, State=Disabled
    "style2=darker|state=disabled": __body3,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default SpinButtonStepper;
