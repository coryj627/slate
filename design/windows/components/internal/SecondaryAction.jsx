import { __slateNorm } from './slateProps.jsx';
import { Chevron2 } from './Chevron2.jsx';

// figma node: 9026:1241 .Secondary action (25 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state);

export function SecondaryAction(_p = {}) {
  _p = __slateNorm(_p, {"style2":["primary","secondary (default)","outline","subtle","transparent"],"state":["rest","hover","pressed","selected","disabled"]});
  const props = { ..._p, focus: _p.focus ?? false, style2: _p.style2 ?? "primary", state: _p.state ?? "rest" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-disabled-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-brand-selected)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-brand-pressed)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-brand-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={32} viewBox="0 0 24 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 20 1 L 20 0 L 20 -1 L 0 -1 L 0 0 Z M 24 4 L 23 4 L 23 28 L 24 28 L 25 28 L 25 4 L 24 4 Z M 20 32 L 20 31 L 0 31 L 0 32 L 0 33 L 20 33 L 20 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 24 28 L 23 28 C 23 29.657 21.657 31 20 31 L 20 32 L 20 33 C 22.761 33 25 30.761 25 28 L 24 28 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-selected)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-pressed)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
          width: 12,
          height: 12,
          flexShrink: 0,
          color: "var(--neutral-foreground-2-hover)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected)",
      borderRight: "2px solid var(--neutral-stroke-1-selected)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={32} viewBox="0 0 24 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 20 1 L 20 0 L 20 -1 L 0 -1 L 0 0 Z M 24 4 L 23 4 L 23 28 L 24 28 L 25 28 L 25 4 L 24 4 Z M 20 32 L 20 31 L 0 31 L 0 32 L 0 33 L 20 33 L 20 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 24 28 L 23 28 C 23 29.657 21.657 31 20 31 L 20 32 L 20 33 C 22.761 33 25 30.761 25 28 L 24 28 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-selected)",
      borderTop: "1px solid var(--neutral-stroke-on-brand-2-selected)",
      borderRight: "1px solid var(--neutral-stroke-on-brand-2-selected)",
      borderBottom: "1px solid var(--neutral-stroke-on-brand-2-selected)",
      borderLeft: "1px solid var(--neutral-stroke-on-brand-2-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-pressed)",
      borderTop: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
      borderRight: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-on-brand-2-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-hover)",
      borderTop: "1px solid var(--neutral-stroke-on-brand-2-hover)",
      borderRight: "1px solid var(--neutral-stroke-on-brand-2-hover)",
      borderBottom: "1px solid var(--neutral-stroke-on-brand-2-hover)",
      borderLeft: "1px solid var(--neutral-stroke-on-brand-2-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--brand-background-1-rest)",
      borderTop: "1px solid var(--neutral-stroke-on-brand-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-on-brand-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-on-brand-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-on-brand-2-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={32} viewBox="0 0 24 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 20 1 L 20 0 L 20 -1 L 0 -1 L 0 0 Z M 24 4 L 23 4 L 23 28 L 24 28 L 25 28 L 25 4 L 24 4 Z M 20 32 L 20 31 L 0 31 L 0 32 L 0 33 L 20 33 L 20 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 24 28 L 23 28 C 23 29.657 21.657 31 20 31 L 20 32 L 20 33 C 22.761 33 25 30.761 25 28 L 24 28 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-pressed)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-hover)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      maxWidth: 24,
      maxHeight: 32,
      borderRadius: "0px 4px 4px 0px",
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
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
        width: 12,
        display: "flex",
        flexDirection: "row",
        padding: "0px 1px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 32,
        overflow: "hidden",
        borderRadius: "0px 4px 4px 0px",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={32} viewBox="0 0 24 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 32,
          overflow: "hidden",
          borderRadius: "0px 4px 4px 0px",
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -1 L -1 -1 L -1 0 L 0 0 Z M 0 32 L -1 32 L -1 33 L 0 33 L 0 32 Z M 0 0 L 0 1 L 20 1 L 20 0 L 20 -1 L 0 -1 L 0 0 Z M 24 4 L 23 4 L 23 28 L 24 28 L 25 28 L 25 4 L 24 4 Z M 20 32 L 20 31 L 0 31 L 0 32 L 0 33 L 20 33 L 20 32 Z M 0 32 L 1 32 L 1 0 L 0 0 L -1 0 L -1 32 L 0 32 Z M 24 28 L 23 28 C 23 29.657 21.657 31 20 31 L 20 32 L 20 33 C 22.761 33 25 30.761 25 28 L 24 28 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Style=Transparent, State=Disabled
    "style2=transparent|state=disabled": __body0,
    // figma: Style=Transparent, State=Selected
    "style2=transparent|state=selected": __body1,
    // figma: Style=Transparent, State=Pressed
    "style2=transparent|state=pressed": __body2,
    // figma: Style=Transparent, State=Hover
    "style2=transparent|state=hover": __body3,
    // figma: Style=Transparent, State=Rest
    "style2=transparent|state=rest": __body4,
    // figma: Style=Subtle, State=Disabled
    "style2=subtle|state=disabled": __body0,
    // figma: Style=Subtle, State=Selected
    "style2=subtle|state=selected": __body5,
    // figma: Style=Subtle, State=Pressed
    "style2=subtle|state=pressed": __body6,
    // figma: Style=Subtle, State=Hover
    "style2=subtle|state=hover": __body7,
    // figma: Style=Subtle, State=Rest
    "style2=subtle|state=rest": __body4,
    // figma: Style=Outline, State=Disabled
    "style2=outline|state=disabled": __body8,
    // figma: Style=Outline, State=Selected
    "style2=outline|state=selected": __body9,
    // figma: Style=Outline, State=Pressed
    "style2=outline|state=pressed": __body10,
    // figma: Style=Outline, State=Hover
    "style2=outline|state=hover": __body11,
    // figma: Style=Outline, State=Rest
    "style2=outline|state=rest": __body12,
    // figma: Style=Primary, State=Disabled
    "style2=primary|state=disabled": __body13,
    // figma: Style=Primary, State=Selected
    "style2=primary|state=selected": __body14,
    // figma: Style=Primary, State=Pressed
    "style2=primary|state=pressed": __body15,
    // figma: Style=Primary, State=Hover
    "style2=primary|state=hover": __body16,
    // figma: Style=Primary, State=Rest
    "style2=primary|state=rest": __body17,
    // figma: Style=Secondary (Default), State=Disabled
    "style2=secondary (default)|state=disabled": __body18,
    // figma: Style=Secondary (Default), State=Selected
    "style2=secondary (default)|state=selected": __body19,
    // figma: Style=Secondary (Default), State=Pressed
    "style2=secondary (default)|state=pressed": __body20,
    // figma: Style=Secondary (Default), State=Hover
    "style2=secondary (default)|state=hover": __body21,
    // figma: Style=Secondary (Default), State=Rest
    "style2=secondary (default)|state=rest": __body22,
  };
  return (__impls[__vkey(props)] ?? __body17)();
}
export default SecondaryAction;
