import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9121:5771 Static_ProgressBar (10 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size);

export function StaticProgressBar(_p = {}) {
  _p = __slateNorm(_p, {"style2":["indeterminate","determinate"],"state":["default","success","error","warning"],"size":["medium (default)","lg"]});
  const props = { ..._p, style2: _p.style2 ?? "indeterminate", state: _p.state ?? "default", size: _p.size ?? "medium (default)" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-success-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-danger-background-3-rest)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-severe-background-3-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-success-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-danger-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-severe-background-3-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 492px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 164,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 2,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={2} viewBox="0 0 164 2" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 2,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 2 L 0 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 328px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 164,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 4,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={4} viewBox="0 0 164 4" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 4,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 4 L 0 4 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Style=Determinate, State=Default, Size=Medium (Default)
    "style2=determinate|state=default|size=medium (default)": __body0,
    // figma: Style=Determinate, State=Success, Size=Medium (Default)
    "style2=determinate|state=success|size=medium (default)": __body1,
    // figma: Style=Determinate, State=Error, Size=Medium (Default)
    "style2=determinate|state=error|size=medium (default)": __body2,
    // figma: Style=Determinate, State=Warning, Size=Medium (Default)
    "style2=determinate|state=warning|size=medium (default)": __body3,
    // figma: Style=Determinate, State=Default, Size=Large
    "style2=determinate|state=default|size=lg": __body4,
    // figma: Style=Determinate, State=Success, Size=Large
    "style2=determinate|state=success|size=lg": __body5,
    // figma: Style=Determinate, State=Error, Size=Large
    "style2=determinate|state=error|size=lg": __body6,
    // figma: Style=Determinate, State=Warning, Size=Large
    "style2=determinate|state=warning|size=lg": __body7,
    // figma: Style=Indeterminate, State=Default, Size=Medium (Default)
    "style2=indeterminate|state=default|size=medium (default)": __body8,
    // figma: Style=Indeterminate, State=Default, Size=Large
    "style2=indeterminate|state=default|size=lg": __body9,
  };
  return (__impls[__vkey(props)] ?? __body8)();
}
export default StaticProgressBar;
