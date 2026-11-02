import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9121:5796 Animated_ProgressBar (38 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state) + '|' + "status=" + __venc(p.status);

export function AnimatedProgressBar(_p = {}) {
  _p = __slateNorm(_p, {"style2":["indeterminate","determinate"],"size":["medium (default)","lg"],"state":["default","success","error","warning"],"status":["not started","in progress","complete","after","before","gradient after","gradient before","99% complete"]});
  const props = { ..._p, style2: _p.style2 ?? "indeterminate", size: _p.size ?? "medium (default)", state: _p.state ?? "default", status: _p.status ?? ((_p.style2 ?? "indeterminate") === "indeterminate" ? "gradient before" : "not started") };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 492px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: -1,
        top: 0,
        width: 1,
        height: 2,
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 491,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 491,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 1px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 491,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 22px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-success-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-danger-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 20px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-severe-background-3-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 492px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: -1,
        top: 0,
        width: 1,
        height: 4,
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 1px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 22px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-success-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-danger-background-3-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 20px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--status-severe-background-3-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 100px 0px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-compound-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 164,
        height: 2,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 2,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={2} viewBox="0 0 164 2" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 2,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 2 L 0 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: -164,
        top: 0,
        width: 164,
        height: 2,
        opacity: 0,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 2,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={2} viewBox="0 0 164 2" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 2,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 2 L 0 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 328,
        top: 0,
        width: 164,
        height: 2,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 2,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={2} viewBox="0 0 164 2" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 2,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 2 L 0 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 2,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 492,
        top: 0,
        width: 164,
        height: 2,
        opacity: 0,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 2,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={2} viewBox="0 0 164 2" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 2,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 2 L 0 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 164,
        height: 4,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 4,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={4} viewBox="0 0 164 4" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 4,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 4 L 0 4 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: -164,
        top: 0,
        width: 164,
        height: 4,
        opacity: 0,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 4,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={4} viewBox="0 0 164 4" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 4,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 4 L 0 4 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 328,
        top: 0,
        width: 164,
        height: 4,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 4,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={4} viewBox="0 0 164 4" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 4,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 4 L 0 4 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 492,
      height: 4,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-6-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 492,
        top: 0,
        width: 164,
        height: 4,
        opacity: 0,
        overflow: "hidden",
        borderRadius: 9999,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 164,
          height: 4,
          clipPath: "inset(0px 0px 0px 0px)",
        }}>
          <svg width={164} height={4} viewBox="0 0 164 4" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 164,
            height: 4,
            color: "var(--brand-background-compound-rest)",
          }}>
            <path d={"M 0 0 L 164 0 L 164 4 L 0 4 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Style=Determinate, Size=Medium (Default), State=Default, Status=Not started
    "style2=determinate|size=medium (default)|state=default|status=not started": __body0,
    // figma: Style=Determinate, Size=Medium (Default), State=Success, Status=Not started
    "style2=determinate|size=medium (default)|state=success|status=not started": __body0,
    // figma: Style=Determinate, Size=Medium (Default), State=Error, Status=Not started
    "style2=determinate|size=medium (default)|state=error|status=not started": __body0,
    // figma: Style=Determinate, Size=Medium (Default), State=Warning, Status=Not started
    "style2=determinate|size=medium (default)|state=warning|status=not started": __body0,
    // figma: Style=Determinate, Size=Medium (Default), State=Default, Status=In progress
    "style2=determinate|size=medium (default)|state=default|status=in progress": __body1,
    // figma: Style=Determinate, Size=Medium (Default), State=Success, Status=In progress
    "style2=determinate|size=medium (default)|state=success|status=in progress": __body1,
    // figma: Style=Determinate, Size=Medium (Default), State=Error, Status=In progress
    "style2=determinate|size=medium (default)|state=error|status=in progress": __body1,
    // figma: Style=Determinate, Size=Medium (Default), State=Warning, Status=In progress
    "style2=determinate|size=medium (default)|state=warning|status=in progress": __body1,
    // figma: Style=Determinate, Size=Medium (Default), State=Default, Status=Complete
    "style2=determinate|size=medium (default)|state=default|status=complete": __body2,
    // figma: Style=Determinate, Size=Medium (Default), State=Success, Status=99% complete
    "style2=determinate|size=medium (default)|state=success|status=99%25 complete": __body3,
    // figma: Style=Determinate, Size=Medium (Default), State=Error, Status=99% complete
    "style2=determinate|size=medium (default)|state=error|status=99%25 complete": __body4,
    // figma: Style=Determinate, Size=Medium (Default), State=Warning, Status=99% complete
    "style2=determinate|size=medium (default)|state=warning|status=99%25 complete": __body5,
    // figma: Style=Determinate, Size=Medium (Default), State=Success, Status=Complete
    "style2=determinate|size=medium (default)|state=success|status=complete": __body6,
    // figma: Style=Determinate, Size=Medium (Default), State=Error, Status=Complete
    "style2=determinate|size=medium (default)|state=error|status=complete": __body7,
    // figma: Style=Determinate, Size=Medium (Default), State=Warning, Status=Complete
    "style2=determinate|size=medium (default)|state=warning|status=complete": __body8,
    // figma: Style=Determinate, Size=Large, State=Default, Status=Not started
    "style2=determinate|size=lg|state=default|status=not started": __body9,
    // figma: Style=Determinate, Size=Large, State=Success, Status=Not started
    "style2=determinate|size=lg|state=success|status=not started": __body9,
    // figma: Style=Determinate, Size=Large, State=Error, Status=Not started
    "style2=determinate|size=lg|state=error|status=not started": __body9,
    // figma: Style=Determinate, Size=Large, State=Warning, Status=Not started
    "style2=determinate|size=lg|state=warning|status=not started": __body9,
    // figma: Style=Determinate, Size=Large, State=Default, Status=Complete
    "style2=determinate|size=lg|state=default|status=complete": __body10,
    // figma: Style=Determinate, Size=Large, State=Success, Status=99% complete
    "style2=determinate|size=lg|state=success|status=99%25 complete": __body10,
    // figma: Style=Determinate, Size=Large, State=Error, Status=99% complete
    "style2=determinate|size=lg|state=error|status=99%25 complete": __body11,
    // figma: Style=Determinate, Size=Large, State=Warning, Status=99% complete
    "style2=determinate|size=lg|state=warning|status=99%25 complete": __body12,
    // figma: Style=Determinate, Size=Large, State=Success, Status=Complete
    "style2=determinate|size=lg|state=success|status=complete": __body13,
    // figma: Style=Determinate, Size=Large, State=Error, Status=Complete
    "style2=determinate|size=lg|state=error|status=complete": __body14,
    // figma: Style=Determinate, Size=Large, State=Warning, Status=Complete
    "style2=determinate|size=lg|state=warning|status=complete": __body15,
    // figma: Style=Determinate, Size=Large, State=Default, Status=In progress
    "style2=determinate|size=lg|state=default|status=in progress": __body16,
    // figma: Style=Determinate, Size=Large, State=Success, Status=In progress
    "style2=determinate|size=lg|state=success|status=in progress": __body16,
    // figma: Style=Determinate, Size=Large, State=Error, Status=In progress
    "style2=determinate|size=lg|state=error|status=in progress": __body16,
    // figma: Style=Determinate, Size=Large, State=Warning, Status=In progress
    "style2=determinate|size=lg|state=warning|status=in progress": __body16,
    // figma: Style=Indeterminate, Size=Medium (Default), State=Default, Status=Gradient before
    "style2=indeterminate|size=medium (default)|state=default|status=gradient before": __body17,
    // figma: Style=Indeterminate, Size=Medium (Default), State=Default, Status=Before
    "style2=indeterminate|size=medium (default)|state=default|status=before": __body18,
    // figma: Style=Indeterminate, Size=Medium (Default), State=Default, Status=Gradient after
    "style2=indeterminate|size=medium (default)|state=default|status=gradient after": __body19,
    // figma: Style=Indeterminate, Size=Medium (Default), State=Default, Status=After
    "style2=indeterminate|size=medium (default)|state=default|status=after": __body20,
    // figma: Style=Indeterminate, Size=Large, State=Default, Status=Gradient before
    "style2=indeterminate|size=lg|state=default|status=gradient before": __body21,
    // figma: Style=Indeterminate, Size=Large, State=Default, Status=Before
    "style2=indeterminate|size=lg|state=default|status=before": __body22,
    // figma: Style=Indeterminate, Size=Large, State=Default, Status=Gradient after
    "style2=indeterminate|size=lg|state=default|status=gradient after": __body23,
    // figma: Style=Indeterminate, Size=Large, State=Default, Status=After
    "style2=indeterminate|size=lg|state=default|status=after": __body24,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default AnimatedProgressBar;
