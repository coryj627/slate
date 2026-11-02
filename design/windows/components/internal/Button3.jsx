import { __slateNorm } from './slateProps.jsx';
import { Icon24StarSmall } from './Icon24StarSmall.jsx';

// figma node: 9738:232 Button (175 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "variant=" + __venc(p.variant) + '|' + "iconLead=" + __venc(p.iconLead) + '|' + "size=" + __venc(p.size) + '|' + "disabled=" + __venc(p.disabled) + '|' + "state=" + __venc(p.state);

export function Button3(_p = {}) {
  _p = __slateNorm(_p, {"variant":["primary","destructive","inverse","success","figjam","secondary","secondary destruct","link","link danger","ghost"],"iconLead":["false","left-aligned","center-aligned"],"size":["default","lg","wide"],"state":["default","active","focused","hover"]});
  const props = { ..._p, variant: _p.variant ?? "primary", iconLead: _p.iconLead ?? "false", label: _p.label ?? "Button", size: _p.size ?? "default", disabled: _p.disabled ?? false, hotkey: _p.hotkey ?? false, state: _p.state ?? "default" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-1) * 1px)",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-1) * 1px)",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-1) * 1px)",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-1) * 1px)",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-disabled-ondisabled)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-figjam-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-disabled-ondisabled)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-danger-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-danger-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px var(--border-danger-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected-strong), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-success-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-disabled-ondisabled)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-disabled-ondisabled)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-selected-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "rgba(0,0,0,0.3)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-figjam-onfigjam)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-figjam-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-figjam-onfigjam)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-figjam-onfigjam)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-danger-ondanger)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-danger-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-danger-ondanger)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-danger-ondanger)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "rgba(255,255,255,0.9)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "rgba(255,255,255,0.9)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-success-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-success-onsuccess)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-success-onsuccess)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-disabled-ondisabled)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-figjam-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-danger-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-danger-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px var(--border-danger-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-success-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-disabled-ondisabled)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-disabled-ondisabled)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-figjam-onfigjam)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body73 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-figjam-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-figjam-onfigjam)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body74 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-figjam)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-figjam-onfigjam)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-figjam-onfigjam)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body75 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-danger-ondanger)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body76 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-danger-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-danger-ondanger)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body77 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-danger)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-danger-ondanger)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-danger-ondanger)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body78 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "rgba(255,255,255,0.9)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body79 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-inverse-default)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "rgba(255,255,255,0.9)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body80 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-success-onsuccess)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body81 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "var(--bg-success-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-success-onsuccess)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body82 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-success)",
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--icon-success-onsuccess)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-onbrand)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body83 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 113,
        top: 4,
        width: 31,
        height: 16,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body84 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-selected-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body85 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.3)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body86 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        height: 16,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body87 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 113,
        top: 4,
        width: 31,
        height: 16,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body88 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        height: 16,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body89 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        height: 16,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body90 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body91 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body92 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-selected-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body93 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-selected-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body94 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.3)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body95 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body96 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body97 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-default-pressed)",
      boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body98 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body99 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-brand-strong), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body100 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-disabled-ondisabled)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body101 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body102 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body103 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body104 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body105 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body106 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body107 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "rgba(0,0,0,0.9)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body108 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body109 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body110 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-brand-strong), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body111 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      backgroundColor: "var(--bg-bg-brand)",
      boxShadow: "inset 0 0 0 1px var(--border-brand-strong), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body112 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-disabled-ondisabled)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-disabled-ondisabled)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body113 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-disabled-ondisabled)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-disabled-ondisabled)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body114 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 110.5,
        top: 4,
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
        }}>{props.label}</span>
      </div>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
    </div>
  );
  const __body115 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 0px 4px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-onbrand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 8,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 500,
          fontSize: 11,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          letterSpacing: "0.005em",
          color: "var(--text-text-onbrand)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body116 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-brand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body117 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-tertiary)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-brand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body118 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-brand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body119 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body120 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-brand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-brand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body121 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-brand-tertiary)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-brand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-brand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body122 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-selected-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-icon-brand)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-brand)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body123 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body124 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-danger-tertiary)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body125 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-danger-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
      {props.hotkey && (
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(255,255,255,0.8)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "⏎"}</span>
      )}
    </div>
  );
  const __body126 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-danger-default)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body127 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "var(--bg-danger-tertiary)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-danger-default)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body128 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-danger-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "var(--icon-danger-default)",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "var(--text-text-danger)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body129 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body130 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body131 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body132 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body133 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body134 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon1 ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body135 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon1 ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body136 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      boxShadow: "0 0 0 1px var(--border-disabled-default)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
          color: "rgba(0,0,0,0.3)",
        }}>{props.icon1 ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body137 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon1 ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body138 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacers-spacer-0) * 1px)",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          height: 24,
          flexShrink: 0,
        }}>{props.icon1 ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.label}</span>
    </div>
  );
  const __body139 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body140 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body141 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body142 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body143 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 12px 4px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingTop: "calc(var(--spacers-spacer-1-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body144 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body145 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body146 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      backgroundColor: "rgba(0,0,0,0.1)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body147 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      overflow: "hidden",
      borderRadius: 5,
      boxShadow: "inset 0 0 0 1px var(--border-border-selected), inset 0px 0px 0px 2px rgb(255,255,255)",
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.9)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __body148 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      borderRadius: 5,
      display: "flex",
      flexDirection: "row",
      padding: "4px 8px 4px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
      paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon ?? <Icon24StarSmall />}</div>
      <span style={{
        position: "relative",
        fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 500,
        fontSize: 11,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        letterSpacing: "0.005em",
        color: "rgba(0,0,0,0.3)",
        flexShrink: 0,
      }}>{props.label}</span>
    </div>
  );
  const __impls = {
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=default|disabled=false|state=default": __body0,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=default|disabled=false|state=active": __body1,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=default|disabled=false|state=focused": __body2,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=default|disabled=true|state=default": __body3,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=default|disabled=false|state=default": __body4,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=default|disabled=false|state=active": __body5,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=default|disabled=false|state=focused": __body6,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=default|disabled=true|state=default": __body7,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=default|disabled=false|state=default": __body8,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=default|disabled=false|state=active": __body9,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=default|disabled=false|state=focused": __body10,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=default|disabled=true|state=default": __body11,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=default|disabled=false|state=default": __body12,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=default|disabled=false|state=active": __body13,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=default|disabled=false|state=focused": __body14,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=default|disabled=true|state=default": __body11,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=default|disabled=false|state=default": __body15,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=default|disabled=false|state=active": __body16,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=default|disabled=false|state=focused": __body17,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=default|disabled=true|state=default": __body18,
    // figma: 👥 Variant=Inverse, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=inverse|iconLead=false|size=default|disabled=false|state=default": __body19,
    // figma: 👥 Variant=Inverse, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=inverse|iconLead=false|size=default|disabled=false|state=focused": __body20,
    // figma: 👥 Variant=Inverse, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=inverse|iconLead=false|size=default|disabled=true|state=default": __body11,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=default|disabled=false|state=default": __body21,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=default|disabled=false|state=active": __body22,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=default|disabled=false|state=focused": __body23,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=default|disabled=true|state=default": __body11,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=default|disabled=false|state=default": __body24,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=default|disabled=false|state=active": __body25,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=default|disabled=false|state=focused": __body26,
    // figma: 👥 Variant=Primary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=default|disabled=true|state=default": __body27,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=default|disabled=false|state=default": __body28,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=default|disabled=false|state=active": __body29,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=default|disabled=false|state=focused": __body30,
    // figma: 👥 Variant=Secondary, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=default|disabled=true|state=default": __body31,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=figjam|iconLead=left-aligned|size=default|disabled=false|state=default": __body32,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=figjam|iconLead=left-aligned|size=default|disabled=false|state=active": __body33,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=figjam|iconLead=left-aligned|size=default|disabled=false|state=focused": __body34,
    // figma: 👥 Variant=FigJam, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=figjam|iconLead=left-aligned|size=default|disabled=true|state=default": __body27,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=destructive|iconLead=left-aligned|size=default|disabled=false|state=default": __body35,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=destructive|iconLead=left-aligned|size=default|disabled=false|state=active": __body36,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=destructive|iconLead=left-aligned|size=default|disabled=false|state=focused": __body37,
    // figma: 👥 Variant=Destructive, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=destructive|iconLead=left-aligned|size=default|disabled=true|state=default": __body27,
    // figma: 👥 Variant=Inverse, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=inverse|iconLead=left-aligned|size=default|disabled=false|state=default": __body38,
    // figma: 👥 Variant=Inverse, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=inverse|iconLead=left-aligned|size=default|disabled=false|state=focused": __body39,
    // figma: 👥 Variant=Inverse, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=inverse|iconLead=left-aligned|size=default|disabled=true|state=default": __body27,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=success|iconLead=left-aligned|size=default|disabled=false|state=default": __body40,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=success|iconLead=left-aligned|size=default|disabled=false|state=active": __body41,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=success|iconLead=left-aligned|size=default|disabled=false|state=focused": __body42,
    // figma: 👥 Variant=Success, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=success|iconLead=left-aligned|size=default|disabled=true|state=default": __body27,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=lg|disabled=false|state=default": __body43,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=lg|disabled=false|state=active": __body44,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=lg|disabled=false|state=focused": __body45,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=lg|disabled=true|state=default": __body46,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=lg|disabled=false|state=default": __body47,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=lg|disabled=false|state=active": __body48,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=lg|disabled=false|state=focused": __body49,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=lg|disabled=true|state=default": __body46,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=lg|disabled=false|state=default": __body50,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=lg|disabled=false|state=active": __body51,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=lg|disabled=false|state=focused": __body52,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=figjam|iconLead=false|size=lg|disabled=true|state=default": __body46,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=lg|disabled=false|state=default": __body53,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=lg|disabled=false|state=active": __body54,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=lg|disabled=false|state=focused": __body55,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=destructive|iconLead=false|size=lg|disabled=true|state=default": __body46,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=lg|disabled=false|state=default": __body56,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=lg|disabled=false|state=active": __body57,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=lg|disabled=false|state=focused": __body58,
    // figma: 👥 Variant=Secondary Destruct, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=secondary destruct|iconLead=false|size=lg|disabled=true|state=default": __body59,
    // figma: 👥 Variant=Inverse, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=inverse|iconLead=false|size=lg|disabled=false|state=default": __body60,
    // figma: 👥 Variant=Inverse, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=inverse|iconLead=false|size=lg|disabled=false|state=focused": __body61,
    // figma: 👥 Variant=Inverse, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=inverse|iconLead=false|size=lg|disabled=true|state=default": __body46,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=lg|disabled=false|state=default": __body62,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=lg|disabled=false|state=active": __body63,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=lg|disabled=false|state=focused": __body64,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=success|iconLead=false|size=lg|disabled=true|state=default": __body46,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=lg|disabled=false|state=default": __body65,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=lg|disabled=false|state=active": __body66,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body67,
    // figma: 👥 Variant=Primary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=lg|disabled=true|state=default": __body68,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=lg|disabled=false|state=default": __body69,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=lg|disabled=false|state=active": __body70,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body71,
    // figma: 👥 Variant=Secondary, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=lg|disabled=true|state=default": __body68,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=figjam|iconLead=center-aligned|size=lg|disabled=false|state=default": __body72,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=figjam|iconLead=center-aligned|size=lg|disabled=false|state=active": __body73,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=figjam|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body74,
    // figma: 👥 Variant=FigJam, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=figjam|iconLead=center-aligned|size=lg|disabled=true|state=default": __body68,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=destructive|iconLead=center-aligned|size=lg|disabled=false|state=default": __body75,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=destructive|iconLead=center-aligned|size=lg|disabled=false|state=active": __body76,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=destructive|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body77,
    // figma: 👥 Variant=Destructive, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=destructive|iconLead=center-aligned|size=lg|disabled=true|state=default": __body68,
    // figma: 👥 Variant=Inverse, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=inverse|iconLead=center-aligned|size=lg|disabled=false|state=default": __body78,
    // figma: 👥 Variant=Inverse, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=inverse|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body79,
    // figma: 👥 Variant=Inverse, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=inverse|iconLead=center-aligned|size=lg|disabled=true|state=default": __body68,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=success|iconLead=center-aligned|size=lg|disabled=false|state=default": __body80,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=success|iconLead=center-aligned|size=lg|disabled=false|state=active": __body81,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=success|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body82,
    // figma: 👥 Variant=Success, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=success|iconLead=center-aligned|size=lg|disabled=true|state=default": __body68,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=wide|disabled=false|state=default": __body83,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=wide|disabled=false|state=focused": __body84,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=wide|disabled=true|state=default": __body85,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=secondary|iconLead=false|size=wide|disabled=false|state=active": __body86,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=wide|disabled=false|state=default": __body87,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=wide|disabled=false|state=focused": __body84,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=wide|disabled=true|state=default": __body85,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=wide|disabled=false|state=active": __body88,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=wide|disabled=false|state=hover": __body89,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=wide|disabled=false|state=default": __body90,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=wide|disabled=false|state=default": __body91,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=wide|disabled=false|state=focused": __body92,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=wide|disabled=false|state=focused": __body93,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=wide|disabled=true|state=default": __body94,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=wide|disabled=true|state=default": __body95,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=secondary|iconLead=left-aligned|size=wide|disabled=false|state=active": __body96,
    // figma: 👥 Variant=Secondary, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=secondary|iconLead=center-aligned|size=wide|disabled=false|state=active": __body97,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=wide|disabled=false|state=default": __body98,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=wide|disabled=false|state=focused": __body99,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=wide|disabled=true|state=default": __body100,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=primary|iconLead=false|size=wide|disabled=false|state=active": __body101,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=wide|disabled=false|state=default": __body102,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=wide|disabled=false|state=default": __body103,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=wide|disabled=false|state=focused": __body92,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=wide|disabled=false|state=focused": __body93,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=wide|disabled=true|state=default": __body94,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=wide|disabled=false|state=active": __body104,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=wide|disabled=false|state=active": __body105,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=wide|disabled=true|state=default": __body95,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=wide|disabled=false|state=hover": __body106,
    // figma: 👥 Variant=Ghost, 👥 Size=Wide, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=wide|disabled=false|state=hover": __body107,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=wide|disabled=false|state=default": __body108,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=wide|disabled=false|state=default": __body109,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=wide|disabled=false|state=focused": __body110,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=wide|disabled=false|state=focused": __body111,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=wide|disabled=true|state=default": __body112,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=wide|disabled=true|state=default": __body113,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=primary|iconLead=left-aligned|size=wide|disabled=false|state=active": __body114,
    // figma: 👥 Variant=Primary, 👥 Size=Wide, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=primary|iconLead=center-aligned|size=wide|disabled=false|state=active": __body115,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=link|iconLead=false|size=default|disabled=false|state=default": __body116,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=link|iconLead=false|size=default|disabled=false|state=active": __body117,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=link|iconLead=false|size=default|disabled=false|state=focused": __body118,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=link|iconLead=false|size=default|disabled=true|state=default": __body119,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=link|iconLead=left-aligned|size=default|disabled=false|state=default": __body120,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=link|iconLead=left-aligned|size=default|disabled=false|state=active": __body121,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=link|iconLead=left-aligned|size=default|disabled=false|state=focused": __body122,
    // figma: 👥 Variant=Link, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=link|iconLead=left-aligned|size=default|disabled=true|state=default": __body31,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=link danger|iconLead=false|size=default|disabled=false|state=default": __body123,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=link danger|iconLead=false|size=default|disabled=false|state=active": __body124,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=link danger|iconLead=false|size=default|disabled=false|state=focused": __body125,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=link danger|iconLead=false|size=default|disabled=true|state=default": __body119,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=link danger|iconLead=left-aligned|size=default|disabled=false|state=default": __body126,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=link danger|iconLead=left-aligned|size=default|disabled=false|state=active": __body127,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=link danger|iconLead=left-aligned|size=default|disabled=false|state=focused": __body128,
    // figma: 👥 Variant=Link Danger, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=link danger|iconLead=left-aligned|size=default|disabled=true|state=default": __body31,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=default|disabled=false|state=default": __body129,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=default|disabled=false|state=active": __body130,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=default|disabled=false|state=hover": __body131,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=default|disabled=false|state=focused": __body132,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=default|disabled=true|state=default": __body133,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=default|disabled=false|state=default": __body134,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=default|disabled=false|state=focused": __body135,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=default|disabled=true|state=default": __body136,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=default|disabled=false|state=active": __body137,
    // figma: 👥 Variant=Ghost, 👥 Size=Default, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=Left-aligned
    "variant=ghost|iconLead=left-aligned|size=default|disabled=false|state=hover": __body138,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=lg|disabled=false|state=default": __body139,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=lg|disabled=false|state=active": __body140,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=lg|disabled=false|state=hover": __body141,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=lg|disabled=false|state=focused": __body142,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=False
    "variant=ghost|iconLead=false|size=lg|disabled=true|state=default": __body143,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=lg|disabled=false|state=default": __body144,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Active, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=lg|disabled=false|state=active": __body145,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Hover, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=lg|disabled=false|state=hover": __body146,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Focused, 🎛️ Disabled=False, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=lg|disabled=false|state=focused": __body147,
    // figma: 👥 Variant=Ghost, 👥 Size=Large, 🐣 State=Default, 🎛️ Disabled=True, 🎛️ Icon Lead=Center-aligned
    "variant=ghost|iconLead=center-aligned|size=lg|disabled=true|state=default": __body148,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Button3;
