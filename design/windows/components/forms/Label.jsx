import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9061:50805 Label (10 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "size=" + __venc(p.size) + '|' + "disabled=" + __venc(p.disabled);

export function Label(_p = {}) {
  _p = __slateNorm(_p, {"type":["regular (default)","semibold"],"size":["sm","md","lg"],"disabled":["off","on"]});
  const props = { ..._p, required: _p.required ?? false, type: _p.type ?? "regular (default)", size: _p.size ?? "sm", disabled: _p.disabled ?? "off", text: _p.text ?? "Label" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
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
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--status-danger-foreground-3-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--status-danger-foreground-3-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
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
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--status-danger-foreground-3-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--status-danger-foreground-3-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
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
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--status-danger-foreground-3-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
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
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text}</span>
      {props.required && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "*"}</span>
      )}
    </div>
  );
  const __impls = {
    // figma: Type=Regular (Default), Size=Small, Disabled=Off
    "type=regular (default)|size=sm|disabled=off": __body0,
    // figma: Type=Semibold, Size=Small, Disabled=Off
    "type=semibold|size=sm|disabled=off": __body1,
    // figma: Type=Regular (Default), Size=Medium, Disabled=Off
    "type=regular (default)|size=md|disabled=off": __body2,
    // figma: Type=Semibold, Size=Medium, Disabled=Off
    "type=semibold|size=md|disabled=off": __body3,
    // figma: Type=Semibold, Size=Large, Disabled=Off
    "type=semibold|size=lg|disabled=off": __body4,
    // figma: Type=Regular (Default), Size=Small, Disabled=On
    "type=regular (default)|size=sm|disabled=on": __body5,
    // figma: Type=Semibold, Size=Small, Disabled=On
    "type=semibold|size=sm|disabled=on": __body6,
    // figma: Type=Regular (Default), Size=Medium, Disabled=On
    "type=regular (default)|size=md|disabled=on": __body7,
    // figma: Type=Semibold, Size=Medium, Disabled=On
    "type=semibold|size=md|disabled=on": __body8,
    // figma: Type=Semibold, Size=Large, Disabled=On
    "type=semibold|size=lg|disabled=on": __body9,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Label;
