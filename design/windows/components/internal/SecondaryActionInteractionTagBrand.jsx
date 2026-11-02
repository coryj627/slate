import { __slateNorm } from './slateProps.jsx';
// figma node: 9112:11184 .Secondary action (Interaction tag - Brand) (7 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state) + '|' + "selected=" + __venc(p.selected);

export function SecondaryActionInteractionTagBrand(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed","disabled"]});
  const props = { ..._p, state: _p.state ?? "rest", selected: _p.selected ?? false, focus: _p.focus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-2-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--brand-foreground-2-rest)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 28 1 L 28 0 L 28 -1 L 0 -1 L 0 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 0 31 L 0 32 L 0 33 L 28 33 L 28 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--neutral-foreground-on-brand-rest)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 28 1 L 28 0 L 28 -1 L 0 -1 L 0 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 0 31 L 0 32 L 0 33 L 28 33 L 28 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-hover)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--neutral-foreground-on-brand-rest)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-pressed)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--neutral-foreground-on-brand-rest)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-2-hover)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--brand-foreground-2-hover)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-2-pressed)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--brand-foreground-2-pressed)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 32,
      maxHeight: null,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--neutral-foreground-disabled-rest)",
        }}>
          <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Rest, Selected=False
    "state=rest|selected=false": __body0,
    // figma: State=Rest, Selected=True
    "state=rest|selected=true": __body1,
    // figma: State=Hover, Selected=True
    "state=hover|selected=true": __body2,
    // figma: State=Pressed, Selected=True
    "state=pressed|selected=true": __body3,
    // figma: State=Hover, Selected=False
    "state=hover|selected=false": __body4,
    // figma: State=Pressed, Selected=False
    "state=pressed|selected=false": __body5,
    // figma: State=Disabled, Selected=False
    "state=disabled|selected=false": __body6,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default SecondaryActionInteractionTagBrand;
