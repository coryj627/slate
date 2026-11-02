import { __slateNorm } from '../internal/slateProps.jsx';
import { Placeholder6 } from '../internal/Placeholder6.jsx';

// figma node: 9230:4902 .Card media (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "aspectRatio=" + __venc(p.aspectRatio);

export function CardMedia(_p = {}) {
  _p = __slateNorm(_p, {"aspectRatio":["16:9","4:3","flex (default)"]});
  const props = { ..._p, checkbox: _p.checkbox ?? false, aspectRatio: _p.aspectRatio ?? "16:9", mediaIcon: _p.mediaIcon ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 300,
      height: 169,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-7f12ea1300756f14" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 300,
        height: 169,
      }} />
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 300,
        height: 169,
        display: "flex",
        flexDirection: "row",
        padding: "12px 12px 12px 12px",
        justifyContent: "space-between",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
      }}>
        {props.mediaIcon && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          alignItems: "flex-end",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            overflow: "hidden",
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "var(--shadow-2)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.pxIcon20 ?? <Placeholder6 size={"20"} theme={"regular"} />}</div>
          </div>
        </div>
        )}
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }} />
        {props.checkbox && (
        <div style={{
          position: "relative",
          width: 16,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
        )}
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 300,
      height: 225,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-7f12ea1300756f14" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 300,
        height: 225,
      }} />
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 300,
        height: 225,
        display: "flex",
        flexDirection: "row",
        padding: "12px 12px 12px 12px",
        justifyContent: "space-between",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
      }}>
        {props.mediaIcon && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          alignItems: "flex-end",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            overflow: "hidden",
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "var(--shadow-2)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.pxIcon20 ?? <Placeholder6 size={"20"} theme={"regular"} />}</div>
          </div>
        </div>
        )}
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }} />
        {props.checkbox && (
        <div style={{
          position: "relative",
          width: 16,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            alignItems: "flex-start",
            flexWrap: "nowrap",
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
        )}
      </div>
    </div>
  );
  const __impls = {
    // figma: Aspect ratio=16:9
    "aspectRatio=16:9": __body0,
    // figma: Aspect ratio=4:3
    "aspectRatio=4:3": __body1,
    // figma: Aspect ratio=Flex (Default)
    "aspectRatio=flex (default)": __body0,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default CardMedia;
