import { __slateNorm } from '../internal/slateProps.jsx';
import { CardBody } from './CardBody.jsx';
import { CardFooter } from './CardFooter.jsx';

// figma node: 9230:4927 Card (28 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "state=" + __venc(p.state) + '|' + "style2=" + __venc(p.style2);

export function Card(_p = {}) {
  _p = __slateNorm(_p, {"layout":["default","custom"],"state":["rest","hover","pressed","draggable","selected","disabled"],"style2":["filled","filled alt","outline","subtle"]});
  const props = { ..._p, layout: _p.layout ?? "default", state: _p.state ?? "rest", style2: _p.style2 ?? "filled", body: _p.body ?? true, footer: _p.footer ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            opacity: 0.8,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            opacity: 0.8,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
                flexShrink: 0,
              }} />
            </div>
          </div>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.1)",
            display: "flex",
            flexDirection: "row",
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
          }}>Copilot is an AI tool designed to improve productivity by integrating with Microsoft applications, offering content generation and task automation features.</span>
        </div>
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            borderRadius: 4,
            backgroundColor: "var(--brand-background-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 2px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-disabled-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Button</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 2px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-disabled-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Button</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
      display: "flex",
      flexDirection: "column",
      padding: "12px 12px 12px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--card-horizontal-2) * 1px)",
      paddingTop: "calc(var(--card-vertical-2) * 1px)",
      paddingRight: "calc(var(--card-horizontal-2) * 1px)",
      paddingBottom: "calc(var(--card-vertical-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <CardBody
        style={{
          position: "relative",
          height: 140,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        customBody={true}
      />
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-2-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-2-hover)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-2-pressed)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-2-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-2-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
      display: "flex",
      flexDirection: "column",
      padding: "12px 12px 12px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--card-horizontal-2) * 1px)",
      paddingTop: "calc(var(--card-vertical-2) * 1px)",
      paddingRight: "calc(var(--card-horizontal-2) * 1px)",
      paddingBottom: "calc(var(--card-vertical-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <CardBody
        style={{
          position: "relative",
          height: 140,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        customBody={true}
      />
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            opacity: 0.8,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            opacity: 0.8,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
                flexShrink: 0,
              }} />
            </div>
          </div>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.1)",
            display: "flex",
            flexDirection: "row",
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
          }}>Copilot is an AI tool designed to improve productivity by integrating with Microsoft applications, offering content generation and task automation features.</span>
        </div>
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            borderRadius: 4,
            backgroundColor: "var(--brand-background-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 2px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-disabled-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Button</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 2px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-disabled-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Button</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "column",
      padding: "12px 12px 12px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--card-horizontal-2) * 1px)",
      paddingTop: "calc(var(--card-vertical-2) * 1px)",
      paddingRight: "calc(var(--card-horizontal-2) * 1px)",
      paddingBottom: "calc(var(--card-vertical-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <CardBody
        style={{
          position: "relative",
          height: 140,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        customBody={true}
      />
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-4)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-3-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
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
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardBody
          style={{
            position: "relative",
            height: 48,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customBody={false}
        />
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <CardFooter
          style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          customFooter={false}
        />
      </div>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
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
        gap: 10,
        padding: "12px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingTop: "calc(var(--card-vertical) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            width: 32,
            height: 32,
            opacity: 0.8,
            borderRadius: 9999,
            borderTop: "1px solid rgba(255,255,255,0)",
            borderRight: "1px solid rgba(255,255,255,0)",
            borderBottom: "1px solid rgba(255,255,255,0)",
            borderLeft: "1px solid rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div className="fig-asset-1ca2b212726726ee" style={{
              position: "relative",
              width: 32,
              borderRadius: 9999,
              borderTop: "1px solid rgba(255,255,255,0)",
              borderRight: "1px solid rgba(255,255,255,0)",
              borderBottom: "1px solid rgba(255,255,255,0)",
              borderLeft: "1px solid rgba(255,255,255,0)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
            <div style={{
              position: "absolute",
              left: 22,
              top: 22,
              borderRadius: 9999,
              backgroundColor: "var(--neutral-background-1-rest)",
              boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
            }}>
              <div style={{
                position: "relative",
                width: 10,
                height: 10,
                flexShrink: 0,
              }}>
                <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  width: 10,
                  height: 10,
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 32,
            height: 32,
            opacity: 0.8,
            flexShrink: 0,
          }}>
            <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 28,
              height: 28,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "20px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Card title</span>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
              lineHeight: "16px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Additional metadata</span>
          </div>
          <div style={{
            position: "relative",
            width: 16,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 16,
                height: 16,
                borderRadius: 2,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
                flexShrink: 0,
              }} />
            </div>
          </div>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0.1)",
            display: "flex",
            flexDirection: "row",
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-gap) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
          }}>Copilot is an AI tool designed to improve productivity by integrating with Microsoft applications, offering content generation and task automation features.</span>
        </div>
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--card-horizontal) * 1px)",
        paddingRight: "calc(var(--card-horizontal) * 1px)",
        paddingBottom: "calc(var(--card-vertical) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
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
            borderRadius: 4,
            backgroundColor: "var(--brand-background-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 2px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-disabled-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Button</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 2px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-disabled-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Button</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "column",
      padding: "12px 12px 12px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--card-horizontal-2) * 1px)",
      paddingTop: "calc(var(--card-vertical-2) * 1px)",
      paddingRight: "calc(var(--card-horizontal-2) * 1px)",
      paddingBottom: "calc(var(--card-vertical-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <CardBody
        style={{
          position: "relative",
          height: 140,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        customBody={true}
      />
    </div>
  );
  const __impls = {
    // figma: Layout=Default, State=Rest, Style=Filled
    "layout=default|state=rest|style2=filled": __body0,
    // figma: Layout=Default, State=Hover, Style=Filled
    "layout=default|state=hover|style2=filled": __body1,
    // figma: Layout=Default, State=Pressed, Style=Filled
    "layout=default|state=pressed|style2=filled": __body2,
    // figma: Layout=Default, State=Selected, Style=Filled
    "layout=default|state=selected|style2=filled": __body3,
    // figma: Layout=Default, State=Draggable, Style=Filled
    "layout=default|state=draggable|style2=filled": __body4,
    // figma: Layout=Default, State=Disabled, Style=Filled
    "layout=default|state=disabled|style2=filled": __body5,
    // figma: Layout=Custom, State=Rest, Style=Filled
    "layout=custom|state=rest|style2=filled": __body6,
    // figma: Layout=Default, State=Rest, Style=Filled alt
    "layout=default|state=rest|style2=filled alt": __body7,
    // figma: Layout=Default, State=Hover, Style=Filled alt
    "layout=default|state=hover|style2=filled alt": __body8,
    // figma: Layout=Default, State=Pressed, Style=Filled alt
    "layout=default|state=pressed|style2=filled alt": __body9,
    // figma: Layout=Default, State=Selected, Style=Filled alt
    "layout=default|state=selected|style2=filled alt": __body10,
    // figma: Layout=Default, State=Draggable, Style=Filled alt
    "layout=default|state=draggable|style2=filled alt": __body4,
    // figma: Layout=Default, State=Disabled, Style=Filled alt
    "layout=default|state=disabled|style2=filled alt": __body5,
    // figma: Layout=Custom, State=Rest, Style=Filled alt
    "layout=custom|state=rest|style2=filled alt": __body11,
    // figma: Layout=Default, State=Rest, Style=Outline
    "layout=default|state=rest|style2=outline": __body12,
    // figma: Layout=Default, State=Hover, Style=Outline
    "layout=default|state=hover|style2=outline": __body13,
    // figma: Layout=Default, State=Pressed, Style=Outline
    "layout=default|state=pressed|style2=outline": __body14,
    // figma: Layout=Default, State=Selected, Style=Outline
    "layout=default|state=selected|style2=outline": __body15,
    // figma: Layout=Default, State=Draggable, Style=Outline
    "layout=default|state=draggable|style2=outline": __body4,
    // figma: Layout=Default, State=Disabled, Style=Outline
    "layout=default|state=disabled|style2=outline": __body16,
    // figma: Layout=Custom, State=Rest, Style=Outline
    "layout=custom|state=rest|style2=outline": __body17,
    // figma: Layout=Default, State=Rest, Style=Subtle
    "layout=default|state=rest|style2=subtle": __body18,
    // figma: Layout=Default, State=Hover, Style=Subtle
    "layout=default|state=hover|style2=subtle": __body19,
    // figma: Layout=Default, State=Pressed, Style=Subtle
    "layout=default|state=pressed|style2=subtle": __body20,
    // figma: Layout=Default, State=Selected, Style=Subtle
    "layout=default|state=selected|style2=subtle": __body21,
    // figma: Layout=Default, State=Draggable, Style=Subtle
    "layout=default|state=draggable|style2=subtle": __body4,
    // figma: Layout=Default, State=Disabled, Style=Subtle
    "layout=default|state=disabled|style2=subtle": __body22,
    // figma: Layout=Custom, State=Rest, Style=Subtle
    "layout=custom|state=rest|style2=subtle": __body23,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Card;
