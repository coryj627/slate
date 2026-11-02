import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9116:27866 Spinner (64 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size);

if (typeof document !== 'undefined' && !document.getElementById('slate-spin-kf')) { const st = document.createElement('style'); st.id = 'slate-spin-kf'; st.textContent = '@keyframes slate-spin{to{transform:rotate(360deg)}}@media (prefers-reduced-motion: reduce){[style*="slate-spin"]{animation-duration:0s!important}}'; document.head.appendChild(st); }
export function Spinner(_p = {}) {
  _p = __slateNorm(_p, {"layout":["label after","label before","label above","label below"],"style2":["primary (default)","subtle"],"size":["extra-tiny","tiny","extra-small","sm","md","lg","extra-large","huge"]});
  const props = { ..._p, layout: _p.layout ?? "label after", style2: _p.style2 ?? "primary (default)", size: _p.size ?? "extra-tiny" };
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
        width: 20,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
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
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
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
        width: 20,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body3 = () => (
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
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body4 = () => (
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
        width: 24,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body5 = () => (
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
        width: 24,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body6 = () => (
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
        width: 28,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body7 = () => (
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
        width: 28,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body8 = () => (
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
        width: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body9 = () => (
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
        width: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body10 = () => (
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
        width: 36,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body11 = () => (
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
        width: 36,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body12 = () => (
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
        width: 40,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body13 = () => (
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
        width: 40,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body14 = () => (
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
        width: 44,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body15 = () => (
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
        width: 44,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: "fit-content",
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body16 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 20,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body17 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body18 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 20,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body19 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body20 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 24,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body21 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 24,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body22 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 28,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body23 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 28,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body24 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body25 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 139,
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 36,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body27 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 36,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 143,
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 40,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body29 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 40,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 163,
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 44,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body31 = () => (
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
        padding: "0px 8px 0px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 44,
        overflow: "hidden",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 36,
        height: 36,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 36,
        height: 36,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 40,
        height: 40,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 40,
        height: 40,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 44,
        height: 44,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
      <div style={{
        position: "relative",
        width: 44,
        height: 44,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        height: 16,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "2px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 9999,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "3px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
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
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 36,
        height: 36,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 36,
        height: 36,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 36,
          height: 36,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
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
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 40,
        height: 40,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
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
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 40,
        height: 40,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
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
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 44,
        height: 44,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid var(--brand-stroke-2-contrast-rest)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--brand-stroke-1-rest)",
          borderRightColor: "var(--brand-stroke-1-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 44,
        height: 44,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid rgba(255,255,255,0.2)",
        }} />
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 44,
          boxSizing: "border-box",
          borderRadius: "50%",
          border: "4px solid transparent",
          borderTopColor: "var(--neutral-stroke-on-brand-2-rest)",
          borderRightColor: "var(--neutral-stroke-on-brand-2-rest)",
          animation: "slate-spin 1.4s cubic-bezier(0.33,0,0.67,1) infinite",
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          whiteSpace: "nowrap",
          lineHeight: "28px",
          color: "var(--neutral-foreground-static-inverted-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "Saving ..."}</span>
      </div>
    </div>
  );
  const __impls = {
    // figma: Layout=Label after, Style=Primary (Default), Size=Tiny
    "layout=label after|style2=primary (default)|size=tiny": __body0,
    // figma: Layout=Label after, Style=Primary (Default), Size=Extra-tiny
    "layout=label after|style2=primary (default)|size=extra-tiny": __body1,
    // figma: Layout=Label after, Style=Subtle, Size=Tiny
    "layout=label after|style2=subtle|size=tiny": __body2,
    // figma: Layout=Label after, Style=Subtle, Size=Extra-tiny
    "layout=label after|style2=subtle|size=extra-tiny": __body3,
    // figma: Layout=Label after, Style=Primary (Default), Size=Extra-small
    "layout=label after|style2=primary (default)|size=extra-small": __body4,
    // figma: Layout=Label after, Style=Subtle, Size=Extra-small
    "layout=label after|style2=subtle|size=extra-small": __body5,
    // figma: Layout=Label after, Style=Primary (Default), Size=Small
    "layout=label after|style2=primary (default)|size=sm": __body6,
    // figma: Layout=Label after, Style=Subtle, Size=Small
    "layout=label after|style2=subtle|size=sm": __body7,
    // figma: Layout=Label after, Style=Primary (Default), Size=Medium
    "layout=label after|style2=primary (default)|size=md": __body8,
    // figma: Layout=Label after, Style=Subtle, Size=Medium
    "layout=label after|style2=subtle|size=md": __body9,
    // figma: Layout=Label after, Style=Primary (Default), Size=Large
    "layout=label after|style2=primary (default)|size=lg": __body10,
    // figma: Layout=Label after, Style=Subtle, Size=Large
    "layout=label after|style2=subtle|size=lg": __body11,
    // figma: Layout=Label after, Style=Primary (Default), Size=Extra-large
    "layout=label after|style2=primary (default)|size=extra-large": __body12,
    // figma: Layout=Label after, Style=Subtle, Size=Extra-large
    "layout=label after|style2=subtle|size=extra-large": __body13,
    // figma: Layout=Label after, Style=Primary (Default), Size=Huge
    "layout=label after|style2=primary (default)|size=huge": __body14,
    // figma: Layout=Label after, Style=Subtle, Size=Huge
    "layout=label after|style2=subtle|size=huge": __body15,
    // figma: Layout=Label before, Style=Primary (Default), Size=Tiny
    "layout=label before|style2=primary (default)|size=tiny": __body16,
    // figma: Layout=Label before, Style=Primary (Default), Size=Extra-tiny
    "layout=label before|style2=primary (default)|size=extra-tiny": __body17,
    // figma: Layout=Label before, Style=Subtle, Size=Tiny
    "layout=label before|style2=subtle|size=tiny": __body18,
    // figma: Layout=Label before, Style=Subtle, Size=Extra-tiny
    "layout=label before|style2=subtle|size=extra-tiny": __body19,
    // figma: Layout=Label before, Style=Primary (Default), Size=Extra-small
    "layout=label before|style2=primary (default)|size=extra-small": __body20,
    // figma: Layout=Label before, Style=Subtle, Size=Extra-small
    "layout=label before|style2=subtle|size=extra-small": __body21,
    // figma: Layout=Label before, Style=Primary (Default), Size=Small
    "layout=label before|style2=primary (default)|size=sm": __body22,
    // figma: Layout=Label before, Style=Subtle, Size=Small
    "layout=label before|style2=subtle|size=sm": __body23,
    // figma: Layout=Label before, Style=Primary (Default), Size=Medium
    "layout=label before|style2=primary (default)|size=md": __body24,
    // figma: Layout=Label before, Style=Subtle, Size=Medium
    "layout=label before|style2=subtle|size=md": __body25,
    // figma: Layout=Label before, Style=Primary (Default), Size=Large
    "layout=label before|style2=primary (default)|size=lg": __body26,
    // figma: Layout=Label before, Style=Subtle, Size=Large
    "layout=label before|style2=subtle|size=lg": __body27,
    // figma: Layout=Label before, Style=Primary (Default), Size=Extra-large
    "layout=label before|style2=primary (default)|size=extra-large": __body28,
    // figma: Layout=Label before, Style=Subtle, Size=Extra-large
    "layout=label before|style2=subtle|size=extra-large": __body29,
    // figma: Layout=Label before, Style=Primary (Default), Size=Huge
    "layout=label before|style2=primary (default)|size=huge": __body30,
    // figma: Layout=Label before, Style=Subtle, Size=Huge
    "layout=label before|style2=subtle|size=huge": __body31,
    // figma: Layout=Label above, Style=Primary (Default), Size=Tiny
    "layout=label above|style2=primary (default)|size=tiny": __body32,
    // figma: Layout=Label above, Style=Primary (Default), Size=Extra-tiny
    "layout=label above|style2=primary (default)|size=extra-tiny": __body33,
    // figma: Layout=Label above, Style=Subtle, Size=Tiny
    "layout=label above|style2=subtle|size=tiny": __body34,
    // figma: Layout=Label above, Style=Subtle, Size=Extra-tiny
    "layout=label above|style2=subtle|size=extra-tiny": __body35,
    // figma: Layout=Label above, Style=Primary (Default), Size=Extra-small
    "layout=label above|style2=primary (default)|size=extra-small": __body36,
    // figma: Layout=Label above, Style=Subtle, Size=Extra-small
    "layout=label above|style2=subtle|size=extra-small": __body37,
    // figma: Layout=Label above, Style=Primary (Default), Size=Small
    "layout=label above|style2=primary (default)|size=sm": __body38,
    // figma: Layout=Label above, Style=Subtle, Size=Small
    "layout=label above|style2=subtle|size=sm": __body39,
    // figma: Layout=Label above, Style=Primary (Default), Size=Medium
    "layout=label above|style2=primary (default)|size=md": __body40,
    // figma: Layout=Label above, Style=Subtle, Size=Medium
    "layout=label above|style2=subtle|size=md": __body41,
    // figma: Layout=Label above, Style=Primary (Default), Size=Large
    "layout=label above|style2=primary (default)|size=lg": __body42,
    // figma: Layout=Label above, Style=Subtle, Size=Large
    "layout=label above|style2=subtle|size=lg": __body43,
    // figma: Layout=Label above, Style=Primary (Default), Size=Extra-large
    "layout=label above|style2=primary (default)|size=extra-large": __body44,
    // figma: Layout=Label above, Style=Subtle, Size=Extra-large
    "layout=label above|style2=subtle|size=extra-large": __body45,
    // figma: Layout=Label above, Style=Primary (Default), Size=Huge
    "layout=label above|style2=primary (default)|size=huge": __body46,
    // figma: Layout=Label above, Style=Subtle, Size=Huge
    "layout=label above|style2=subtle|size=huge": __body47,
    // figma: Layout=Label below, Style=Primary (Default), Size=Tiny
    "layout=label below|style2=primary (default)|size=tiny": __body48,
    // figma: Layout=Label below, Style=Primary (Default), Size=Extra-tiny
    "layout=label below|style2=primary (default)|size=extra-tiny": __body49,
    // figma: Layout=Label below, Style=Subtle, Size=Tiny
    "layout=label below|style2=subtle|size=tiny": __body50,
    // figma: Layout=Label below, Style=Subtle, Size=Extra-tiny
    "layout=label below|style2=subtle|size=extra-tiny": __body51,
    // figma: Layout=Label below, Style=Primary (Default), Size=Extra-small
    "layout=label below|style2=primary (default)|size=extra-small": __body52,
    // figma: Layout=Label below, Style=Subtle, Size=Extra-small
    "layout=label below|style2=subtle|size=extra-small": __body53,
    // figma: Layout=Label below, Style=Primary (Default), Size=Small
    "layout=label below|style2=primary (default)|size=sm": __body54,
    // figma: Layout=Label below, Style=Subtle, Size=Small
    "layout=label below|style2=subtle|size=sm": __body55,
    // figma: Layout=Label below, Style=Primary (Default), Size=Medium
    "layout=label below|style2=primary (default)|size=md": __body56,
    // figma: Layout=Label below, Style=Subtle, Size=Medium
    "layout=label below|style2=subtle|size=md": __body57,
    // figma: Layout=Label below, Style=Primary (Default), Size=Large
    "layout=label below|style2=primary (default)|size=lg": __body58,
    // figma: Layout=Label below, Style=Subtle, Size=Large
    "layout=label below|style2=subtle|size=lg": __body59,
    // figma: Layout=Label below, Style=Primary (Default), Size=Extra-large
    "layout=label below|style2=primary (default)|size=extra-large": __body60,
    // figma: Layout=Label below, Style=Subtle, Size=Extra-large
    "layout=label below|style2=subtle|size=extra-large": __body61,
    // figma: Layout=Label below, Style=Primary (Default), Size=Huge
    "layout=label below|style2=primary (default)|size=huge": __body62,
    // figma: Layout=Label below, Style=Subtle, Size=Huge
    "layout=label below|style2=subtle|size=huge": __body63,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default Spinner;
