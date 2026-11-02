import { __slateNorm } from './slateProps.jsx';
// figma node: 9064:5795 .[DEPRECATED] Divider (40 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "style2=" + __venc(p.style2) + '|' + "inset=" + __venc(p.inset) + '|' + "contentAlign=" + __venc(p.contentAlign) + '|' + "orientation=" + __venc(p.orientation);

export function DEPRECATEDDivider(_p = {}) {
  _p = __slateNorm(_p, {"layout":["default","with content","with icon"],"style2":["default","subtle","strong","brand"],"inset":["block (default)","inset"],"contentAlign":["center (default)","start","end"],"orientation":["horizontal"]});
  const props = { ..._p, layout: _p.layout ?? "default", style2: _p.style2 ?? "default", inset: _p.inset ?? "block (default)", contentAlign: _p.contentAlign ?? "center (default)", orientation: _p.orientation ?? "horizontal" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 0px 4px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      color: "var(--neutral-stroke-2-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 600 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 600 0 L 600 -0.5 L 600 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 0px 4px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      color: "var(--neutral-stroke-3-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 600 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 600 0 L 600 -0.5 L 600 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 0px 4px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      color: "var(--neutral-stroke-1-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 600 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 600 0 L 600 -0.5 L 600 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 0px 4px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      color: "var(--brand-stroke-1-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 600 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 600 0 L 600 -0.5 L 600 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--brand-foreground-1-rest-4)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 266.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266.5 0 L 266.5 -0.5 L 266.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 278 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 278 0 L 278 -0.5 L 278 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 12px 4px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      color: "var(--neutral-stroke-2-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 576 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 576 0 L 576 -0.5 L 576 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 12px 4px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      color: "var(--neutral-stroke-3-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 576 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 576 0 L 576 -0.5 L 576 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 12px 4px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      color: "var(--neutral-stroke-1-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 576 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 576 0 L 576 -0.5 L 576 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "5px 12px 4px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-1-rest-3)",
      ...props.style,
    }}>
      <svg height={1} viewBox="0 -0.500 576 1" fill="none" style={{
        position: "relative",
        height: 1,
        borderWidth: "calc(var(--stroke-width-thin) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 0 -0.5 L 0 0 L 576 0 L 576 -0.5 L 576 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--brand-foreground-1-rest-4)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 254.500 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 254.5 0 L 254.5 -0.5 L 254.5 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      padding: "0px 12px 0px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 266 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 266 0 L 266 -0.5 L 266 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--brand-foreground-1-rest-4)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 600,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-2-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-3-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest-3)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--neutral-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 525 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 525 0 L 525 -0.5 L 525 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--brand-foreground-1-rest-4)",
              flexShrink: 0,
            }}>Content</span>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 600,
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
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg viewBox="0 -0.500 548 1" fill="none" style={{
            position: "relative",
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 548 0 L 548 -0.5 L 548 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            padding: "0px 12px 0px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                flexShrink: 0,
                alignSelf: "stretch",
                color: "var(--neutral-foreground-2-rest-3)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <svg width={8} viewBox="0 -0.500 8 1" fill="none" style={{
            position: "relative",
            width: 8,
            borderWidth: "calc(var(--stroke-width-thin) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
            color: "var(--brand-stroke-1-rest-3)",
          }}>
            <path d={"M 0 -0.5 L 0 0 L 8 0 L 8 -0.5 L 8 -1 L 0 -1 L 0 -0.5 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Layout=Default, Style=Default, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=default|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body0,
    // figma: Layout=With content, Style=Default, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=default|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body1,
    // figma: Layout=With icon, Style=Default, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=default|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body2,
    // figma: Layout=Default, Style=Subtle, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=subtle|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body3,
    // figma: Layout=With content, Style=Subtle, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=subtle|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body4,
    // figma: Layout=With icon, Style=Subtle, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=subtle|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body5,
    // figma: Layout=Default, Style=Strong, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=strong|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body6,
    // figma: Layout=With content, Style=Strong, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=strong|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body7,
    // figma: Layout=With icon, Style=Strong, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=strong|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body8,
    // figma: Layout=Default, Style=Brand, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=brand|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body9,
    // figma: Layout=With content, Style=Brand, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=brand|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body10,
    // figma: Layout=With icon, Style=Brand, Inset=Block (default), Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=brand|inset=block (default)|contentAlign=center (default)|orientation=horizontal": __body11,
    // figma: Layout=Default, Style=Default, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=default|inset=inset|contentAlign=center (default)|orientation=horizontal": __body12,
    // figma: Layout=With content, Style=Default, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=default|inset=inset|contentAlign=center (default)|orientation=horizontal": __body13,
    // figma: Layout=With icon, Style=Default, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=default|inset=inset|contentAlign=center (default)|orientation=horizontal": __body14,
    // figma: Layout=Default, Style=Subtle, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=subtle|inset=inset|contentAlign=center (default)|orientation=horizontal": __body15,
    // figma: Layout=With content, Style=Subtle, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=subtle|inset=inset|contentAlign=center (default)|orientation=horizontal": __body16,
    // figma: Layout=With icon, Style=Subtle, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=subtle|inset=inset|contentAlign=center (default)|orientation=horizontal": __body17,
    // figma: Layout=Default, Style=Strong, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=strong|inset=inset|contentAlign=center (default)|orientation=horizontal": __body18,
    // figma: Layout=With content, Style=Strong, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=strong|inset=inset|contentAlign=center (default)|orientation=horizontal": __body19,
    // figma: Layout=With icon, Style=Strong, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=strong|inset=inset|contentAlign=center (default)|orientation=horizontal": __body20,
    // figma: Layout=Default, Style=Brand, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=default|style2=brand|inset=inset|contentAlign=center (default)|orientation=horizontal": __body21,
    // figma: Layout=With content, Style=Brand, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with content|style2=brand|inset=inset|contentAlign=center (default)|orientation=horizontal": __body22,
    // figma: Layout=With icon, Style=Brand, Inset=Inset, Content align=Center (default), Orientation=Horizontal
    "layout=with icon|style2=brand|inset=inset|contentAlign=center (default)|orientation=horizontal": __body23,
    // figma: Layout=With content, Style=Default, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with content|style2=default|inset=block (default)|contentAlign=start|orientation=horizontal": __body24,
    // figma: Layout=With icon, Style=Default, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with icon|style2=default|inset=block (default)|contentAlign=start|orientation=horizontal": __body25,
    // figma: Layout=With content, Style=Subtle, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with content|style2=subtle|inset=block (default)|contentAlign=start|orientation=horizontal": __body26,
    // figma: Layout=With icon, Style=Subtle, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with icon|style2=subtle|inset=block (default)|contentAlign=start|orientation=horizontal": __body27,
    // figma: Layout=With content, Style=Strong, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with content|style2=strong|inset=block (default)|contentAlign=start|orientation=horizontal": __body28,
    // figma: Layout=With icon, Style=Strong, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with icon|style2=strong|inset=block (default)|contentAlign=start|orientation=horizontal": __body29,
    // figma: Layout=With content, Style=Brand, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with content|style2=brand|inset=block (default)|contentAlign=start|orientation=horizontal": __body30,
    // figma: Layout=With icon, Style=Brand, Inset=Block (default), Content align=Start, Orientation=Horizontal
    "layout=with icon|style2=brand|inset=block (default)|contentAlign=start|orientation=horizontal": __body31,
    // figma: Layout=With content, Style=Default, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with content|style2=default|inset=block (default)|contentAlign=end|orientation=horizontal": __body32,
    // figma: Layout=With icon, Style=Default, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with icon|style2=default|inset=block (default)|contentAlign=end|orientation=horizontal": __body33,
    // figma: Layout=With content, Style=Subtle, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with content|style2=subtle|inset=block (default)|contentAlign=end|orientation=horizontal": __body34,
    // figma: Layout=With icon, Style=Subtle, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with icon|style2=subtle|inset=block (default)|contentAlign=end|orientation=horizontal": __body35,
    // figma: Layout=With content, Style=Strong, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with content|style2=strong|inset=block (default)|contentAlign=end|orientation=horizontal": __body36,
    // figma: Layout=With icon, Style=Strong, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with icon|style2=strong|inset=block (default)|contentAlign=end|orientation=horizontal": __body37,
    // figma: Layout=With content, Style=Brand, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with content|style2=brand|inset=block (default)|contentAlign=end|orientation=horizontal": __body38,
    // figma: Layout=With icon, Style=Brand, Inset=Block (default), Content align=End, Orientation=Horizontal
    "layout=with icon|style2=brand|inset=block (default)|contentAlign=end|orientation=horizontal": __body39,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default DEPRECATEDDivider;
