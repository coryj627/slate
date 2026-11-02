import { __slateNorm } from '../internal/slateProps.jsx';
import { InfoButton } from './InfoButton.jsx';

// figma node: 9066:900 InfoLabel (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size);

export function InfoLabel(_p = {}) {
  _p = __slateNorm(_p, {"size":["md","sm","lg"]});
  const props = { ..._p, size: _p.size ?? "md" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
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
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <InfoButton size={"md"} state={"rest"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
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
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>Label</span>
      </div>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <InfoButton size={"lg"} state={"rest"} />}</div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
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
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>Label</span>
      </div>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <InfoButton size={"sm"} state={"rest"} />}</div>
    </div>
  );
  const __impls = {
    // figma: Size=Medium
    "size=md": __body0,
    // figma: Size=Large
    "size=lg": __body1,
    // figma: Size=Small
    "size=sm": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default InfoLabel;
