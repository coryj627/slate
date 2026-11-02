import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9380:3505 .CarouselNav (4 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "mode=" + __venc(p.mode);

export function CarouselNav(_p = {}) {
  _p = __slateNorm(_p, {"mode":["stepsdefault","stepsautoplay","imagepreviewautoplay","imagepreview default"]});
  const props = { ..._p, mode: _p.mode ?? "stepsdefault" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 24,
          borderRadius: 4,
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 20,
                height: 20,
                overflow: "hidden",
                flexShrink: 0,
              }}>
                <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                  position: "absolute",
                  left: 2,
                  top: 2,
                  width: 16,
                  height: 16,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 8 1 C 4.134 1 1 4.134 1 8 C 1 11.866 4.134 15 8 15 C 11.866 15 15 11.866 15 8 C 15 4.134 11.866 1 8 1 Z M 6.5 4.5 C 6.776 4.5 7 4.724 7 5 L 7 11 C 7 11.276 6.776 11.5 6.5 11.5 C 6.224 11.5 6 11.276 6 11 L 6 5 C 6 4.724 6.224 4.5 6.5 4.5 Z M 9.5 4.5 C 9.776 4.5 10 4.724 10 5 L 10 11 C 10 11.276 9.776 11.5 9.5 11.5 C 9.224 11.5 9 11.276 9 11 L 9 5 C 9 4.724 9.224 4.5 9.5 4.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 20,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 7,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 8.53 0.22 C 8.823 0.513 8.823 0.987 8.53 1.28 L 1.811 8 L 8.53 14.72 C 8.823 15.013 8.823 15.487 8.53 15.78 C 8.237 16.073 7.763 16.073 7.47 15.78 L 0.22 8.53 C -0.073 8.237 -0.073 7.763 0.22 7.47 L 7.47 0.22 C 7.763 -0.073 8.237 -0.073 8.53 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 8,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-xs) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 20,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 8.25,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 0.22 0.22 C -0.073 0.513 -0.073 0.987 0.22 1.28 L 6.939 8 L 0.22 14.72 C -0.073 15.013 -0.073 15.487 0.22 15.78 C 0.513 16.073 0.987 16.073 1.28 15.78 L 8.53 8.53 C 8.823 8.237 8.823 7.763 8.53 7.47 L 1.28 0.22 C 0.987 -0.073 0.513 -0.073 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 20,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 7,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 8.53 0.22 C 8.823 0.513 8.823 0.987 8.53 1.28 L 1.811 8 L 8.53 14.72 C 8.823 15.013 8.823 15.487 8.53 15.78 C 8.237 16.073 7.763 16.073 7.47 15.78 L 0.22 8.53 C -0.073 8.237 -0.073 7.763 0.22 7.47 L 7.47 0.22 C 7.763 -0.073 8.237 -0.073 8.53 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 8,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-xs) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "8px 8px 8px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-s) * 1px)",
          paddingTop: "calc(var(--vertical-s) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-s) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 8,
            opacity: 0.3,
            borderRadius: 9999,
            backgroundColor: "var(--rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 20,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 8.25,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 0.22 0.22 C -0.073 0.513 -0.073 0.987 0.22 1.28 L 6.939 8 L 0.22 14.72 C -0.073 15.013 -0.073 15.487 0.22 15.78 C 0.513 16.073 0.987 16.073 1.28 15.78 L 8.53 8.53 C 8.823 8.237 8.823 7.763 8.53 7.47 L 1.28 0.22 C 0.987 -0.073 0.513 -0.073 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          width: 24,
          height: 24,
          borderRadius: 4,
          flexShrink: 0,
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 20,
                height: 20,
                overflow: "hidden",
                flexShrink: 0,
              }}>
                <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                  position: "absolute",
                  left: 2,
                  top: 2,
                  width: 16,
                  height: 16,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 8 1 C 4.134 1 1 4.134 1 8 C 1 11.866 4.134 15 8 15 C 11.866 15 15 11.866 15 8 C 15 4.134 11.866 1 8 1 Z M 6.5 4.5 C 6.776 4.5 7 4.724 7 5 L 7 11 C 7 11.276 6.776 11.5 6.5 11.5 C 6.224 11.5 6 11.276 6 11 L 6 5 C 6 4.724 6.224 4.5 6.5 4.5 Z M 9.5 4.5 C 9.776 4.5 10 4.724 10 5 L 10 11 C 10 11.276 9.776 11.5 9.5 11.5 C 9.224 11.5 9 11.276 9 11 L 9 5 C 9 4.724 9.224 4.5 9.5 4.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 20,
              height: 20,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 7,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 8.53 0.22 C 8.823 0.513 8.823 0.987 8.53 1.28 L 1.811 8 L 8.53 14.72 C 8.823 15.013 8.823 15.487 8.53 15.78 C 8.237 16.073 7.763 16.073 7.47 15.78 L 0.22 8.53 C -0.073 8.237 -0.073 7.763 0.22 7.47 L 7.47 0.22 C 7.763 -0.073 8.237 -0.073 8.53 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 24,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 48,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 48,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>48x48</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 20,
              height: 20,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 8.25,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 0.22 0.22 C -0.073 0.513 -0.073 0.987 0.22 1.28 L 6.939 8 L 0.22 14.72 C -0.073 15.013 -0.073 15.487 0.22 15.78 C 0.513 16.073 0.987 16.073 1.28 15.78 L 8.53 8.53 C 8.823 8.237 8.823 7.763 8.53 7.47 L 1.28 0.22 C 0.987 -0.073 0.513 -0.073 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 20,
              height: 20,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 7,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 8.53 0.22 C 8.823 0.513 8.823 0.987 8.53 1.28 L 1.811 8 L 8.53 14.72 C 8.823 15.013 8.823 15.487 8.53 15.78 C 8.237 16.073 7.763 16.073 7.47 15.78 L 0.22 8.53 C -0.073 8.237 -0.073 7.763 0.22 7.47 L 7.47 0.22 C 7.763 -0.073 8.237 -0.073 8.53 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 24,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 48,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 48,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>48x48</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 40,
          height: 40,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 40,
            overflow: "hidden",
            borderRadius: 2,
            backgroundColor: "var(--brand-background-2-rest)",
            boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
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
              height: 22,
              display: "flex",
              flexDirection: "column",
              gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
              padding: "4px 0px 4px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 700,
                fontSize: 10,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "14px",
                textBox: "trim-both cap alphabetic",
                letterSpacing: "0.030em",
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>40x40</span>
              <span style={{
                position: "relative",
                width: 32,
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 4,
                textAlign: "center",
                lineHeight: 1,
                color: "var(--brand-foreground-2-rest)",
                flexShrink: 0,
              }}>Use component swap to change</span>
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 20,
              height: 20,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
                position: "absolute",
                left: 8.25,
                top: 4,
                width: 8.75,
                height: 16,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 0.22 0.22 C -0.073 0.513 -0.073 0.987 0.22 1.28 L 6.939 8 L 0.22 14.72 C -0.073 15.013 -0.073 15.487 0.22 15.78 C 0.513 16.073 0.987 16.073 1.28 15.78 L 8.53 8.53 C 8.823 8.237 8.823 7.763 8.53 7.47 L 1.28 0.22 C 0.987 -0.073 0.513 -0.073 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Mode=StepsAutoPlay
    "mode=stepsautoplay": __body0,
    // figma: Mode=StepsDefault
    "mode=stepsdefault": __body1,
    // figma: Mode=ImagePreviewAutoPlay
    "mode=imagepreviewautoplay": __body2,
    // figma: Mode=ImagePreview default
    "mode=imagepreview default": __body3,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default CarouselNav;
