import { __slateNorm } from './slateProps.jsx';
import { Chevron2 } from './Chevron2.jsx';

// figma node: 9121:6376 .Item split button (5 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state);

export function ItemSplitButton(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed","selected","disabled"]});
  const props = { ..._p, state: _p.state ?? "rest", focus: _p.focus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 25,
      height: 32,
      minWidth: 25,
      maxWidth: 25,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
        }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 1,
        height: 24,
        backgroundColor: "var(--neutral-stroke-1-rest)",
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 1,
        top: 0,
        width: 24,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={24} height={32} viewBox="0 0 24 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 20 1 L 20 0 L 20 -1 L 0 -1 L 0 0 Z M 24 4 L 23 4 L 23 28 L 24 28 L 25 28 L 25 4 L 24 4 Z M 20 32 L 20 31 L 0 31 L 0 32 L 0 33 L 20 33 L 20 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 24 28 L 23 28 C 23 29.657 21.657 31 20 31 L 20 32 L 20 33 C 22.761 33 25 30.761 25 28 L 24 28 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 25,
      height: 32,
      minWidth: 25,
      maxWidth: 25,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"filled"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 1,
        height: 24,
        backgroundColor: "var(--neutral-stroke-1-hover)",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 25,
      height: 32,
      minWidth: 25,
      maxWidth: 25,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-1-pressed)",
        }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"filled"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 1,
        height: 24,
        backgroundColor: "var(--neutral-stroke-1-pressed)",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 25,
      height: 32,
      minWidth: 25,
      maxWidth: 25,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-selected)",
        }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 1,
        height: 24,
        backgroundColor: "var(--neutral-stroke-1-selected)",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 25,
      height: 32,
      minWidth: 25,
      maxWidth: 25,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-disabled-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 1,
        height: 24,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __impls = {
    // figma: State=Rest
    "state=rest": __body0,
    // figma: State=Hover
    "state=hover": __body1,
    // figma: State=Pressed
    "state=pressed": __body2,
    // figma: State=Selected
    "state=selected": __body3,
    // figma: State=Disabled
    "state=disabled": __body4,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default ItemSplitButton;
