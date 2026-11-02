import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9121:2771 Slider (10 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state);

export function Slider(_p = {}) {
  _p = __slateNorm(_p, {"size":["medium (default)","sm"],"state":["rest","hover","pressed","disabled","focus"]});
  const props = { ..._p, ticks: _p.ticks ?? false, size: _p.size ?? "medium (default)", state: _p.state ?? "rest" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 120,
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 4,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={4} viewBox="0 0 104 4" fill="none" style={{
            position: "relative",
            height: 4,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 102 0 C 103.105 0 104 0.895 104 2 L 104 2 C 104 3.105 103.105 4 102 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 51px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 4,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={4} viewBox="0 0 43 4" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 4,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 43 0 L 43 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 7,
            width: 120,
            height: 4,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 18,
            height: 18,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={12} viewBox="0 0 12 12" fill="none" style={{
              position: "relative",
              width: 12,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 6 C 0 2.686 2.686 0 6 0 L 6 0 C 9.314 0 12 2.686 12 6 L 12 6 C 12 9.314 9.314 12 6 12 L 6 12 C 2.686 12 0 9.314 0 6 L 0 6 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 120,
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 4,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={4} viewBox="0 0 104 4" fill="none" style={{
            position: "relative",
            height: 4,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 102 0 C 103.105 0 104 0.895 104 2 L 104 2 C 104 3.105 103.105 4 102 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 51px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 4,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={4} viewBox="0 0 43 4" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 4,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-hover)",
            }}>
              <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 43 0 L 43 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 7,
            width: 120,
            height: 4,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 18,
            height: 18,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={12} viewBox="0 0 12 12" fill="none" style={{
              position: "relative",
              width: 12,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-hover)",
            }}>
              <path d={"M 0 6 C 0 2.686 2.686 0 6 0 L 6 0 C 9.314 0 12 2.686 12 6 L 12 6 C 12 9.314 9.314 12 6 12 L 6 12 C 2.686 12 0 9.314 0 6 L 0 6 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 120,
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 4,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={4} viewBox="0 0 104 4" fill="none" style={{
            position: "relative",
            height: 4,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 102 0 C 103.105 0 104 0.895 104 2 L 104 2 C 104 3.105 103.105 4 102 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 51px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 4,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={4} viewBox="0 0 43 4" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 4,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-pressed)",
            }}>
              <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 43 0 L 43 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 7,
            width: 120,
            height: 4,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 18,
            height: 18,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={12} viewBox="0 0 12 12" fill="none" style={{
              position: "relative",
              width: 12,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-pressed)",
            }}>
              <path d={"M 0 6 C 0 2.686 2.686 0 6 0 L 6 0 C 9.314 0 12 2.686 12 6 L 12 6 C 12 9.314 9.314 12 6 12 L 6 12 C 2.686 12 0 9.314 0 6 L 0 6 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 120,
      borderRadius: 2,
      boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 4,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={4} viewBox="0 0 104 4" fill="none" style={{
            position: "relative",
            height: 4,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 102 0 C 103.105 0 104 0.895 104 2 L 104 2 C 104 3.105 103.105 4 102 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 51px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 4,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={4} viewBox="0 0 43 4" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 4,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 43 0 L 43 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 7,
            width: 120,
            height: 4,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 18,
            height: 18,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={12} viewBox="0 0 12 12" fill="none" style={{
              position: "relative",
              width: 12,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 6 C 0 2.686 2.686 0 6 0 L 6 0 C 9.314 0 12 2.686 12 6 L 12 6 C 12 9.314 9.314 12 6 12 L 6 12 C 2.686 12 0 9.314 0 6 L 0 6 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 120,
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 4,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 4,
            borderRadius: 2,
            backgroundColor: "rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            padding: "10px 10px 10px 10px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            paddingTop: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            flexGrow: 1,
          }} />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 51px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 4,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={4} viewBox="0 0 43 4" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 4,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 0 2 C 0 0.895 0.895 0 2 0 L 43 0 L 43 4 L 2 4 C 0.895 4 0 3.105 0 2 L 0 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 7,
            width: 120,
            height: 4,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 18,
            height: 18,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-disabled-rest)",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={12} viewBox="0 0 12 12" fill="none" style={{
              position: "relative",
              width: 12,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 0 6 C 0 2.686 2.686 0 6 0 L 6 0 C 9.314 0 12 2.686 12 6 L 12 6 C 12 9.314 9.314 12 6 12 L 6 12 C 2.686 12 0 9.314 0 6 L 0 6 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 120,
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 2,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={2} viewBox="0 0 104 2" fill="none" style={{
            position: "relative",
            height: 2,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 103 0 C 103.552 0 104 0.448 104 1 L 104 1 C 104 1.552 103.552 2 103 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 5,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 53px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={2} viewBox="0 0 45 2" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 2,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 44 0 C 44.552 0 45 0.448 45 1 L 45 1 C 45 1.552 44.552 2 44 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 6,
            width: 120,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 14,
            height: 14,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={10} viewBox="0 0 10 10" fill="none" style={{
              position: "relative",
              width: 10,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 5 C 0 2.239 2.239 0 5 0 L 5 0 C 7.761 0 10 2.239 10 5 L 10 5 C 10 7.761 7.761 10 5 10 L 5 10 C 2.239 10 0 7.761 0 5 L 0 5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 2,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={2} viewBox="0 0 104 2" fill="none" style={{
            position: "relative",
            height: 2,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 103 0 C 103.552 0 104 0.448 104 1 L 104 1 C 104 1.552 103.552 2 103 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 5,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 53px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={2} viewBox="0 0 45 2" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 2,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-hover)",
            }}>
              <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 44 0 C 44.552 0 45 0.448 45 1 L 45 1 C 45 1.552 44.552 2 44 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 6,
            width: 120,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 14,
            height: 14,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={10} viewBox="0 0 10 10" fill="none" style={{
              position: "relative",
              width: 10,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-hover)",
            }}>
              <path d={"M 0 5 C 0 2.239 2.239 0 5 0 L 5 0 C 7.761 0 10 2.239 10 5 L 10 5 C 10 7.761 7.761 10 5 10 L 5 10 C 2.239 10 0 7.761 0 5 L 0 5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 2,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={2} viewBox="0 0 104 2" fill="none" style={{
            position: "relative",
            height: 2,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 103 0 C 103.552 0 104 0.448 104 1 L 104 1 C 104 1.552 103.552 2 103 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 5,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 53px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={2} viewBox="0 0 45 2" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 2,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-pressed)",
            }}>
              <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 44 0 C 44.552 0 45 0.448 45 1 L 45 1 C 45 1.552 44.552 2 44 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 6,
            width: 120,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 14,
            height: 14,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={10} viewBox="0 0 10 10" fill="none" style={{
              position: "relative",
              width: 10,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-pressed)",
            }}>
              <path d={"M 0 5 C 0 2.239 2.239 0 5 0 L 5 0 C 7.761 0 10 2.239 10 5 L 10 5 C 10 7.761 7.761 10 5 10 L 5 10 C 2.239 10 0 7.761 0 5 L 0 5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-focus-1-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 2,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg height={2} viewBox="0 0 104 2" fill="none" style={{
            position: "relative",
            height: 2,
            borderRadius: 2,
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
            color: "var(--neutral-stroke-accessible-rest)",
          }}>
            <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 103 0 C 103.552 0 104 0.448 104 1 L 104 1 C 104 1.552 103.552 2 103 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 5,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 53px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={2} viewBox="0 0 45 2" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 2,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 44 0 C 44.552 0 45 0.448 45 1 L 45 1 C 45 1.552 44.552 2 44 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 6,
            width: 120,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 14,
            height: 14,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={10} viewBox="0 0 10 10" fill="none" style={{
              position: "relative",
              width: 10,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--brand-background-compound-rest)",
            }}>
              <path d={"M 0 5 C 0 2.239 2.239 0 5 0 L 5 0 C 7.761 0 10 2.239 10 5 L 10 5 C 10 7.761 7.761 10 5 10 L 5 10 C 2.239 10 0 7.761 0 5 L 0 5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 2,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 2,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 2,
          display: "flex",
          flexDirection: "row",
          padding: "12px 8px 12px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            borderRadius: 2,
            backgroundColor: "rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            padding: "10px 10px 10px 10px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            paddingTop: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            flexGrow: 1,
          }} />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 5,
          width: 120,
          display: "flex",
          flexDirection: "row",
          padding: "0px 53px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: "0px 8px 0px 8px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexGrow: 1,
          }}>
            <svg height={2} viewBox="0 0 45 2" fill="none" style={{
              position: "relative",
              transform: "matrix(1,0,0,-1,0,0)",
              height: 2,
              borderRadius: 2,
              display: "flex",
              flexDirection: "row",
              gap: 10,
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 44 0 C 44.552 0 45 0.448 45 1 L 45 1 C 45 1.552 44.552 2 44 2 L 1 2 C 0.448 2 0 1.552 0 1 L 0 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          {props.ticks && (
          <div style={{
            position: "absolute",
            left: 1,
            top: 6,
            width: 120,
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "nowrap",
          }}>
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 1,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
            <div style={{
              position: "relative",
              transform: "matrix(-1,0,0,1,0,0)",
              width: 0.01,
              height: 2,
              backgroundColor: "var(--colors-neutral-white)",
              flexShrink: 0,
            }} />
          </div>
          )}
          <div style={{
            position: "relative",
            width: 14,
            height: 14,
            borderRadius: 15,
            backgroundColor: "var(--neutral-background-1-rest)",
            boxShadow: "0 0 0 1px var(--neutral-stroke-disabled-rest)",
            display: "flex",
            flexDirection: "row",
            padding: "3px 3px 3px 3px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <svg width={10} viewBox="0 0 10 10" fill="none" style={{
              position: "relative",
              width: 10,
              borderRadius: 15,
              display: "flex",
              flexDirection: "column",
              gap: 10,
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 0 5 C 0 2.239 2.239 0 5 0 L 5 0 C 7.761 0 10 2.239 10 5 L 10 5 C 10 7.761 7.761 10 5 10 L 5 10 C 2.239 10 0 7.761 0 5 L 0 5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=Medium (Default), State=Rest
    "size=medium (default)|state=rest": __body0,
    // figma: Size=Medium (Default), State=Hover
    "size=medium (default)|state=hover": __body1,
    // figma: Size=Medium (Default), State=Pressed
    "size=medium (default)|state=pressed": __body2,
    // figma: Size=Medium (Default), State=Focus
    "size=medium (default)|state=focus": __body3,
    // figma: Size=Medium (Default), State=Disabled
    "size=medium (default)|state=disabled": __body4,
    // figma: Size=Small, State=Rest
    "size=sm|state=rest": __body5,
    // figma: Size=Small, State=Hover
    "size=sm|state=hover": __body6,
    // figma: Size=Small, State=Pressed
    "size=sm|state=pressed": __body7,
    // figma: Size=Small, State=Focus
    "size=sm|state=focus": __body8,
    // figma: Size=Small, State=Disabled
    "size=sm|state=disabled": __body9,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Slider;
