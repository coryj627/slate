import { __slateNorm } from './slateProps.jsx';
import { Checkmark2 } from './Checkmark2.jsx';

// figma node: 9328:24853 .Selection (Small) (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type);

export function SelectionSmall(_p = {}) {
  _p = __slateNorm(_p, {"type":["checkbox","checkmark","radio"]});
  const props = { ..._p, type: _p.type ?? "checkbox" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      padding: "5px 5px 5px 5px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 14,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 14,
        overflow: "hidden",
        borderRadius: 2,
        backgroundColor: "var(--brand-background-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={7.500} height={5.500} viewBox="0 0 7.500 5.500" fill="none" style={{
          position: "absolute",
          left: 3.5,
          top: 4,
          width: 7.5,
          height: 5.5,
          color: "var(--neutral-foreground-static-inverted-rest)",
        }}>
          <path d={"M 7.354 0.146 C 7.549 0.342 7.549 0.658 7.354 0.854 L 2.854 5.354 C 2.658 5.549 2.342 5.549 2.146 5.354 L 0.146 3.354 C -0.049 3.158 -0.049 2.842 0.146 2.646 C 0.342 2.451 0.658 2.451 0.854 2.646 L 2.5 4.293 L 6.646 0.146 C 6.842 -0.049 7.158 -0.049 7.354 0.146 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      color: "var(--brand-stroke-compound-rest)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 14,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 14,
          height: 14,
        }}>
          <path d={"M 14 7 L 13 7 C 13 10.314 10.314 13 7 13 L 7 14 L 7 15 C 11.418 15 15 11.418 15 7 L 14 7 Z M 7 14 L 7 13 C 3.686 13 1 10.314 1 7 L 0 7 L -1 7 C -1 11.418 2.582 15 7 15 L 7 14 Z M 0 7 L 1 7 C 1 3.686 3.686 1 7 1 L 7 0 L 7 -1 C 2.582 -1 -1 2.582 -1 7 L 0 7 Z M 7 0 L 7 1 C 10.314 1 13 3.686 13 7 L 14 7 L 15 7 C 15 2.582 11.418 -1 7 -1 L 7 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <div style={{
          position: "absolute",
          left: 3,
          top: 3,
          width: 8,
          height: 8,
          borderRadius: "50%",
          backgroundColor: "var(--brand-background-compound-rest)",
        }} />
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
export default SelectionSmall;
