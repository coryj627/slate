import { __slateNorm } from '../internal/slateProps.jsx';
import { Placeholder } from '../internal/Placeholder.jsx';

// figma node: 9291:4177 App item - small (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state);

export function AppItemSmall(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed"]});
  const props = { ..._p, state: _p.state ?? "rest", text: _p.text ?? "App item name" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 252,
      height: 40,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-4-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-mnudge) * 1px)",
      padding: "8px 6px 8px 14px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-1-rest)",
        }}>{props.iconBefore ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        width: 198,
        display: "flex",
        flexDirection: "row",
        gap: 10,
        padding: "0px 4px 0px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          width: 194,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text}</span>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 252,
      height: 40,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-4-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-mnudge) * 1px)",
      padding: "8px 6px 8px 14px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-1-hover)",
        }}>{props.iconBefore ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        width: 202,
        display: "flex",
        flexDirection: "row",
        gap: 10,
        padding: "0px 4px 0px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          width: 198,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text}</span>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 252,
      height: 40,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-4-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-mnudge) * 1px)",
      padding: "8px 6px 8px 14px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-1-pressed)",
        }}>{props.iconBefore ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        width: 198,
        display: "flex",
        flexDirection: "row",
        gap: 10,
        padding: "0px 4px 0px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          width: 194,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text}</span>
      </div>
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
export default AppItemSmall;
