import { __slateNorm } from './slateProps.jsx';
import { Checkmark2 } from './Checkmark2.jsx';

// figma node: 9328:24846 .Selection (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type);

export function Selection(_p = {}) {
  _p = __slateNorm(_p, {"type":["checkbox","checkmark","radio"]});
  const props = { ..._p, type: _p.type ?? "checkbox" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      borderRadius: 2,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 16,
          height: 16,
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      borderRadius: 2,
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
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
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
            borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
            flexShrink: 0,
          }} />
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      borderRadius: 2,
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
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              color: "var(--brand-stroke-compound-rest-3)",
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
              backgroundColor: "var(--brand-foreground-compound-rest-3)",
              boxShadow: "inset 0 0 0 1px var(--brand-stroke-compound-rest-3)",
            }} />
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Type=Checkmark
    "type=checkmark": __body0,
    // figma: Type=Checkbox
    "type=checkbox": __body1,
    // figma: Type=Radio
    "type=radio": __body2,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default Selection;
