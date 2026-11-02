import { __slateNorm } from '../internal/slateProps.jsx';
import { SpinButtonStepper } from '../internal/SpinButtonStepper.jsx';

// figma node: 9116:28347 Spin button (56 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size);

export function SpinButton(_p = {}) {
  _p = __slateNorm(_p, {"style2":["outline (default)","filled darker","filled lighter","underline"],"state":["rest","hover","pressed","selected","error","disabled","read only"],"size":["medium (default)","sm"]});
  const props = { ..._p, style2: _p.style2 ?? "outline (default)", state: _p.state ?? "rest", size: _p.size ?? "medium (default)" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <div style={{
        position: "absolute",
        left: 70,
        top: 30,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 70,
        top: 30,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 70,
        top: 30,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <div style={{
        position: "absolute",
        left: 70,
        top: 30,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 30,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--status-danger-stroke-2-rest)",
      }} />
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 31,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <div style={{
        position: "absolute",
        left: 70,
        top: 22,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 70,
        top: 22,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "inset 0 0 0 1px var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"darker"} state={"disabled"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 70,
        top: 22,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <div style={{
        position: "absolute",
        left: 70,
        top: 22,
        width: 140,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-selected)",
      }} />
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-3-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"rest"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--status-danger-stroke-2-rest)",
      }} />
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>Placeholder text</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
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
            overflow: "hidden",
            display: "flex",
            flexDirection: "row",
            padding: "4px 6px 0px 6px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 3,
                width: 8,
                height: 4.5,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <div style={{
              position: "relative",
              flexGrow: 1,
              alignSelf: "stretch",
              height: "auto",
              width: "auto",
            }}>{props.icon2 ?? <SpinButtonStepper style2={"default"} state={"disabled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 23,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-disabled-rest)",
      }} />
    </div>
  );
  const __impls = {
    // figma: Style=Outline (Default), State=Rest, Size=Medium (Default)
    "style2=outline (default)|state=rest|size=medium (default)": __body0,
    // figma: Style=Outline (Default), State=Hover, Size=Medium (Default)
    "style2=outline (default)|state=hover|size=medium (default)": __body1,
    // figma: Style=Outline (Default), State=Pressed, Size=Medium (Default)
    "style2=outline (default)|state=pressed|size=medium (default)": __body2,
    // figma: Style=Outline (Default), State=Selected, Size=Medium (Default)
    "style2=outline (default)|state=selected|size=medium (default)": __body3,
    // figma: Style=Outline (Default), State=Error, Size=Medium (Default)
    "style2=outline (default)|state=error|size=medium (default)": __body4,
    // figma: Style=Outline (Default), State=Disabled, Size=Medium (Default)
    "style2=outline (default)|state=disabled|size=medium (default)": __body5,
    // figma: Style=Outline (Default), State=Read only, Size=Medium (Default)
    "style2=outline (default)|state=read only|size=medium (default)": __body6,
    // figma: Style=Filled darker, State=Rest, Size=Medium (Default)
    "style2=filled darker|state=rest|size=medium (default)": __body7,
    // figma: Style=Filled darker, State=Hover, Size=Medium (Default)
    "style2=filled darker|state=hover|size=medium (default)": __body7,
    // figma: Style=Filled darker, State=Pressed, Size=Medium (Default)
    "style2=filled darker|state=pressed|size=medium (default)": __body8,
    // figma: Style=Filled darker, State=Selected, Size=Medium (Default)
    "style2=filled darker|state=selected|size=medium (default)": __body9,
    // figma: Style=Filled darker, State=Error, Size=Medium (Default)
    "style2=filled darker|state=error|size=medium (default)": __body10,
    // figma: Style=Filled darker, State=Disabled, Size=Medium (Default)
    "style2=filled darker|state=disabled|size=medium (default)": __body11,
    // figma: Style=Filled darker, State=Read only, Size=Medium (Default)
    "style2=filled darker|state=read only|size=medium (default)": __body12,
    // figma: Style=Filled lighter, State=Rest, Size=Medium (Default)
    "style2=filled lighter|state=rest|size=medium (default)": __body13,
    // figma: Style=Filled lighter, State=Hover, Size=Medium (Default)
    "style2=filled lighter|state=hover|size=medium (default)": __body13,
    // figma: Style=Filled lighter, State=Pressed, Size=Medium (Default)
    "style2=filled lighter|state=pressed|size=medium (default)": __body14,
    // figma: Style=Filled lighter, State=Selected, Size=Medium (Default)
    "style2=filled lighter|state=selected|size=medium (default)": __body15,
    // figma: Style=Filled lighter, State=Error, Size=Medium (Default)
    "style2=filled lighter|state=error|size=medium (default)": __body4,
    // figma: Style=Filled lighter, State=Disabled, Size=Medium (Default)
    "style2=filled lighter|state=disabled|size=medium (default)": __body5,
    // figma: Style=Filled lighter, State=Read only, Size=Medium (Default)
    "style2=filled lighter|state=read only|size=medium (default)": __body6,
    // figma: Style=Underline, State=Rest, Size=Medium (Default)
    "style2=underline|state=rest|size=medium (default)": __body16,
    // figma: Style=Underline, State=Hover, Size=Medium (Default)
    "style2=underline|state=hover|size=medium (default)": __body17,
    // figma: Style=Underline, State=Pressed, Size=Medium (Default)
    "style2=underline|state=pressed|size=medium (default)": __body18,
    // figma: Style=Underline, State=Selected, Size=Medium (Default)
    "style2=underline|state=selected|size=medium (default)": __body19,
    // figma: Style=Underline, State=Error, Size=Medium (Default)
    "style2=underline|state=error|size=medium (default)": __body20,
    // figma: Style=Underline, State=Disabled, Size=Medium (Default)
    "style2=underline|state=disabled|size=medium (default)": __body21,
    // figma: Style=Underline, State=Read only, Size=Medium (Default)
    "style2=underline|state=read only|size=medium (default)": __body22,
    // figma: Style=Outline (Default), State=Rest, Size=Small
    "style2=outline (default)|state=rest|size=sm": __body23,
    // figma: Style=Outline (Default), State=Hover, Size=Small
    "style2=outline (default)|state=hover|size=sm": __body24,
    // figma: Style=Outline (Default), State=Pressed, Size=Small
    "style2=outline (default)|state=pressed|size=sm": __body25,
    // figma: Style=Outline (Default), State=Selected, Size=Small
    "style2=outline (default)|state=selected|size=sm": __body26,
    // figma: Style=Outline (Default), State=Error, Size=Small
    "style2=outline (default)|state=error|size=sm": __body27,
    // figma: Style=Outline (Default), State=Disabled, Size=Small
    "style2=outline (default)|state=disabled|size=sm": __body28,
    // figma: Style=Outline (Default), State=Read only, Size=Small
    "style2=outline (default)|state=read only|size=sm": __body29,
    // figma: Style=Filled darker, State=Rest, Size=Small
    "style2=filled darker|state=rest|size=sm": __body30,
    // figma: Style=Filled darker, State=Hover, Size=Small
    "style2=filled darker|state=hover|size=sm": __body30,
    // figma: Style=Filled darker, State=Pressed, Size=Small
    "style2=filled darker|state=pressed|size=sm": __body31,
    // figma: Style=Filled darker, State=Selected, Size=Small
    "style2=filled darker|state=selected|size=sm": __body32,
    // figma: Style=Filled darker, State=Error, Size=Small
    "style2=filled darker|state=error|size=sm": __body33,
    // figma: Style=Filled darker, State=Disabled, Size=Small
    "style2=filled darker|state=disabled|size=sm": __body34,
    // figma: Style=Filled darker, State=Read only, Size=Small
    "style2=filled darker|state=read only|size=sm": __body35,
    // figma: Style=Filled lighter, State=Rest, Size=Small
    "style2=filled lighter|state=rest|size=sm": __body36,
    // figma: Style=Filled lighter, State=Hover, Size=Small
    "style2=filled lighter|state=hover|size=sm": __body36,
    // figma: Style=Filled lighter, State=Pressed, Size=Small
    "style2=filled lighter|state=pressed|size=sm": __body37,
    // figma: Style=Filled lighter, State=Selected, Size=Small
    "style2=filled lighter|state=selected|size=sm": __body38,
    // figma: Style=Filled lighter, State=Error, Size=Small
    "style2=filled lighter|state=error|size=sm": __body27,
    // figma: Style=Filled lighter, State=Disabled, Size=Small
    "style2=filled lighter|state=disabled|size=sm": __body28,
    // figma: Style=Filled lighter, State=Read only, Size=Small
    "style2=filled lighter|state=read only|size=sm": __body29,
    // figma: Style=Underline, State=Rest, Size=Small
    "style2=underline|state=rest|size=sm": __body39,
    // figma: Style=Underline, State=Hover, Size=Small
    "style2=underline|state=hover|size=sm": __body40,
    // figma: Style=Underline, State=Pressed, Size=Small
    "style2=underline|state=pressed|size=sm": __body41,
    // figma: Style=Underline, State=Selected, Size=Small
    "style2=underline|state=selected|size=sm": __body42,
    // figma: Style=Underline, State=Error, Size=Small
    "style2=underline|state=error|size=sm": __body43,
    // figma: Style=Underline, State=Disabled, Size=Small
    "style2=underline|state=disabled|size=sm": __body44,
    // figma: Style=Underline, State=Read only, Size=Small
    "style2=underline|state=read only|size=sm": __body45,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default SpinButton;
