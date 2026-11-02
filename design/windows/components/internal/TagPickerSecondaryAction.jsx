import { __slateNorm } from './slateProps.jsx';
// figma node: 9064:15390 TagPicker/Secondary action (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state);

export function TagPickerSecondaryAction(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed"]});
  const props = { ..._p, state: _p.state ?? "rest", secondaryString: _p.secondaryString ?? "Secondary action" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-3-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.secondaryString}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-3-hover)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.secondaryString}</span>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-3-pressed)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.secondaryString}</span>
    </div>
  );
  const __impls = {
    // figma: State=Rest
    "state=rest": __body0,
    // figma: State=Hover
    "state=hover": __body1,
    // figma: State=Pressed
    "state=pressed": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default TagPickerSecondaryAction;
