import { __slateNorm } from '../internal/slateProps.jsx';
import { CarouselNav } from './CarouselNav.jsx';
import { TitleBodyCopy } from '../internal/TitleBodyCopy.jsx';

// figma node: 9380:3615 Carousel (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "chevronPlacement=" + __venc(p.chevronPlacement) + '|' + "pauseButton=" + __venc(p.pauseButton) + '|' + "navType=" + __venc(p.navType);

export function Carousel(_p = {}) {
  _p = __slateNorm(_p, {"layout":["over content","outside content"],"chevronPlacement":["flexible to edges","grouped to steps","centered to content"],"pauseButton":["in nav","on content click"],"navType":["steps","image preview"]});
  const props = { ..._p, layout: _p.layout ?? "over content", chevronPlacement: _p.chevronPlacement ?? "flexible to edges", pauseButton: _p.pauseButton ?? ((_p.layout ?? "over content") === "over content" ? "on content click" : "in nav"), navType: _p.navType ?? "steps", title: _p.title ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy
        style={{ position: "relative", flexShrink: 0 }}
        showSubheader={true}
      />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{ position: "absolute", left: 111.5, top: 309 }}
        mode={"stepsdefault"}
      />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{ position: "absolute", left: 27.5, top: 309 }}
        mode={"imagepreviewautoplay"}
      />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <div style={{
        position: "absolute",
        left: 111.5,
        top: 261,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
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
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.5)",
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
          borderRadius: 8,
          backgroundColor: "rgba(255,255,255,0.5)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
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
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 8,
                borderRadius: 9999,
                backgroundColor: "var(--rest)",
                flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.5)",
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
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 415,
      height: 297,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy
        style={{ position: "relative", flexShrink: 0 }}
        showSubheader={true}
      />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{ position: "absolute", left: 95.5, top: 309 }}
        mode={"stepsautoplay"}
      />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 0,
          top: 309,
          width: 415,
        }}
        mode={"stepsdefault"}
      />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 147.5,
          top: 309,
          width: 120,
        }}
        mode={"stepsdefault"}
      />
      <CarouselNav
        style={{
          position: "absolute",
          left: -36,
          top: 162,
          width: 487,
        }}
        mode={"stepsdefault"}
      />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 0,
          top: 309,
          width: 415,
        }}
        mode={"imagepreviewautoplay"}
      />
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <div style={{
        position: "absolute",
        left: 12,
        top: 261,
        width: 391,
        display: "flex",
        flexDirection: "row",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
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
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.5)",
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
          borderRadius: 8,
          backgroundColor: "rgba(255,255,255,0.5)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
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
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 8,
                borderRadius: 9999,
                backgroundColor: "var(--rest)",
                flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.5)",
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
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <div style={{
        position: "absolute",
        left: 12,
        top: 162,
        width: 391,
        display: "flex",
        flexDirection: "row",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          backgroundColor: "rgba(255,255,255,0.5)",
          display: "flex",
          flexDirection: "row",
          gap: 8,
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
          borderRadius: 8,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
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
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 8,
                borderRadius: 9999,
                backgroundColor: "var(--rest)",
                flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
            }} />
          </div>
        </div>
        <div style={{
          position: "relative",
          backgroundColor: "rgba(255,255,255,0.5)",
          display: "flex",
          flexDirection: "row",
          gap: 8,
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
      <div style={{
        position: "absolute",
        left: 148,
        top: 261,
        borderRadius: 8,
        backgroundColor: "rgba(255,255,255,0.5)",
        display: "flex",
        flexDirection: "row",
        gap: 12,
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 8,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
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
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 8,
                borderRadius: 9999,
                backgroundColor: "var(--rest)",
                flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
            }} />
          </div>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 0,
          top: 309,
          width: 415,
        }}
        mode={"stepsautoplay"}
      />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 79.5,
          top: 309,
          width: 256,
        }}
        mode={"imagepreview default"}
      />
      <CarouselNav
        style={{
          position: "absolute",
          left: -35,
          top: 162,
          width: 487,
          height: 24,
        }}
        mode={"imagepreview default"}
      />
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 79.5,
          top: 309,
          width: 256,
        }}
        mode={"imagepreview default"}
      />
      <div style={{
        position: "absolute",
        left: 12,
        top: 162,
        width: 391,
        display: "flex",
        flexDirection: "row",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          backgroundColor: "rgba(255,255,255,0.5)",
          display: "flex",
          flexDirection: "row",
          gap: 8,
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
          borderRadius: 8,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
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
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 8,
                borderRadius: 9999,
                backgroundColor: "var(--rest)",
                flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 8,
              height: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--rest)",
              flexShrink: 0,
            }} />
          </div>
        </div>
        <div style={{
          position: "relative",
          backgroundColor: "rgba(255,255,255,0.5)",
          display: "flex",
          flexDirection: "row",
          gap: 8,
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
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 415,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.title && (
      <TitleBodyCopy style={{ position: "relative", flexShrink: 0 }} />
      )}
      <div style={{
        position: "relative",
        height: 245,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.250px var(--brand-foreground-1-rest), 0 0 0 0.250px var(--brand-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 125,
          top: 116,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      <CarouselNav
        style={{
          position: "absolute",
          left: 79.5,
          top: 309,
          width: 256,
        }}
        mode={"imagepreview default"}
      />
      <div style={{
        position: "absolute",
        left: 12,
        top: 261,
        width: 391,
        display: "flex",
        flexDirection: "row",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          backgroundColor: "rgba(255,255,255,0.5)",
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
        }}>
          <div style={{
            position: "relative",
            width: 48,
            height: 48,
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
          backgroundColor: "rgba(255,255,255,0.5)",
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
    </div>
  );
  const __impls = {
    // figma: Layout=Outside content, Chevron placement=Grouped to steps, Pause Button=On content click, Nav Type=Steps
    "layout=outside content|chevronPlacement=grouped to steps|pauseButton=on content click|navType=steps": __body0,
    // figma: Layout=Outside content, Chevron placement=Grouped to steps, Pause Button=On content click, Nav Type=Image preview
    "layout=outside content|chevronPlacement=grouped to steps|pauseButton=on content click|navType=image preview": __body1,
    // figma: Layout=Over content, Chevron placement=Grouped to steps, Pause Button=On content click, Nav Type=Steps
    "layout=over content|chevronPlacement=grouped to steps|pauseButton=on content click|navType=steps": __body2,
    // figma: Layout=Outside content, Chevron placement=Grouped to steps, Pause Button=In nav, Nav Type=Steps
    "layout=outside content|chevronPlacement=grouped to steps|pauseButton=in nav|navType=steps": __body3,
    // figma: Layout=Outside content, Chevron placement=Grouped to steps, Pause Button=In nav, Nav Type=Image preview
    "layout=outside content|chevronPlacement=grouped to steps|pauseButton=in nav|navType=image preview": __body1,
    // figma: Layout=Outside content, Chevron placement=Flexible to edges, Pause Button=On content click, Nav Type=Steps
    "layout=outside content|chevronPlacement=flexible to edges|pauseButton=on content click|navType=steps": __body4,
    // figma: Layout=Outside content, Chevron placement=Centered to content, Pause Button=On content click, Nav Type=Steps
    "layout=outside content|chevronPlacement=centered to content|pauseButton=on content click|navType=steps": __body5,
    // figma: Layout=Outside content, Chevron placement=Flexible to edges, Pause Button=On content click, Nav Type=Image preview
    "layout=outside content|chevronPlacement=flexible to edges|pauseButton=on content click|navType=image preview": __body6,
    // figma: Layout=Over content, Chevron placement=Flexible to edges, Pause Button=On content click, Nav Type=Steps
    "layout=over content|chevronPlacement=flexible to edges|pauseButton=on content click|navType=steps": __body7,
    // figma: Layout=Over content, Chevron placement=Centered to content, Pause Button=On content click, Nav Type=Steps
    "layout=over content|chevronPlacement=centered to content|pauseButton=on content click|navType=steps": __body8,
    // figma: Layout=Outside content, Chevron placement=Flexible to edges, Pause Button=In nav, Nav Type=Steps
    "layout=outside content|chevronPlacement=flexible to edges|pauseButton=in nav|navType=steps": __body9,
    // figma: Layout=Outside content, Chevron placement=Flexible to edges, Pause Button=In nav, Nav Type=Image preview
    "layout=outside content|chevronPlacement=flexible to edges|pauseButton=in nav|navType=image preview": __body6,
    // figma: Layout=Outside content, Chevron placement=Centered to content, Pause Button=On content click, Nav Type=Image preview
    "layout=outside content|chevronPlacement=centered to content|pauseButton=on content click|navType=image preview": __body10,
    // figma: Layout=Over content, Chevron placement=Centered to content, Pause Button=On content click, Nav Type=Image preview
    "layout=over content|chevronPlacement=centered to content|pauseButton=on content click|navType=image preview": __body11,
    // figma: Layout=Over content, Chevron placement=Flexible to edges, Pause Button=On content click, Nav Type=Image preview
    "layout=over content|chevronPlacement=flexible to edges|pauseButton=on content click|navType=image preview": __body12,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Carousel;
