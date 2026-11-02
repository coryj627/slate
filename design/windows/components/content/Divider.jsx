import { __slateNorm } from '../internal/slateProps.jsx';
import { Placeholder4 } from '../internal/Placeholder4.jsx';

// figma node: 9000:6131 Divider (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "vertical=" + __venc(p.vertical);

export function Divider(_p = {}) {
  _p = __slateNorm(_p, {"layout":["center (default)","start","end"]});
  const props = { ..._p, contentSlot: _p.contentSlot ?? true, layout: _p.layout ?? "center (default)", vertical: _p.vertical ?? false, text: _p.text ?? true, textString: _p.textString ?? "Text", icon: _p.icon ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-padding) * 1px)",
      paddingRight: "calc(var(--horizontal-padding) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
      {props.contentSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "0px 12px 0px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxIcon20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
        {props.text && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--content-color)",
          flexShrink: 0,
        }}>{props.textString}</span>
        )}
      </div>
      )}
      <div style={{
        position: "relative",
        height: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-padding) * 1px)",
      paddingRight: "calc(var(--horizontal-padding) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        height: 1,
        backgroundColor: "var(--stroke-color)",
        flexShrink: 0,
      }} />
      {props.contentSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "0px 12px 0px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxIcon20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
        {props.text && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--content-color)",
          flexShrink: 0,
        }}>{props.textString}</span>
        )}
      </div>
      )}
      <div style={{
        position: "relative",
        height: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 280,
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-padding) * 1px)",
      paddingRight: "calc(var(--horizontal-padding) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
      {props.contentSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "0px 12px 0px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.pxIcon20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
        {props.text && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--content-color)",
          flexShrink: 0,
        }}>{props.textString}</span>
        )}
      </div>
      )}
      <div style={{
        position: "relative",
        width: 8,
        height: 1,
        backgroundColor: "var(--stroke-color)",
        flexShrink: 0,
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 84,
      display: "flex",
      flexDirection: "column",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingTop: "calc(var(--vertical-padding) * 1px)",
      paddingBottom: "calc(var(--vertical-padding) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
      {props.contentSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 0px 12px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.pxIcon20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
        {props.text && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--content-color)",
          flexShrink: 0,
        }}>{props.textString}</span>
        )}
      </div>
      )}
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 84,
      display: "flex",
      flexDirection: "column",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingTop: "calc(var(--vertical-padding) * 1px)",
      paddingBottom: "calc(var(--vertical-padding) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 1,
        height: 8,
        backgroundColor: "var(--stroke-color)",
        flexShrink: 0,
      }} />
      {props.contentSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 0px 12px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.pxIcon20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
        {props.text && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--content-color)",
          flexShrink: 0,
        }}>{props.textString}</span>
        )}
      </div>
      )}
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 84,
      display: "flex",
      flexDirection: "column",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingTop: "calc(var(--vertical-padding) * 1px)",
      paddingBottom: "calc(var(--vertical-padding) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--stroke-color)",
        flexGrow: 1,
      }} />
      {props.contentSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 0px 12px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.pxIcon20 ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        )}
        {props.text && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--content-color)",
          flexShrink: 0,
        }}>{props.textString}</span>
        )}
      </div>
      )}
      <div style={{
        position: "relative",
        width: 1,
        height: 8,
        backgroundColor: "var(--stroke-color)",
        flexShrink: 0,
      }} />
    </div>
  );
  const __impls = {
    // figma: Layout=Center (Default), Vertical=False
    "layout=center (default)|vertical=false": __body0,
    // figma: Layout=Start, Vertical=False
    "layout=start|vertical=false": __body1,
    // figma: Layout=End, Vertical=False
    "layout=end|vertical=false": __body2,
    // figma: Layout=Center (Default), Vertical=True
    "layout=center (default)|vertical=true": __body3,
    // figma: Layout=Start, Vertical=True
    "layout=start|vertical=true": __body4,
    // figma: Layout=End, Vertical=True
    "layout=end|vertical=true": __body5,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Divider;
