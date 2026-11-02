import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron } from '../internal/Chevron.jsx';
import { DEPRECATEDDivider } from '../internal/DEPRECATEDDivider.jsx';
import { Dropdown } from '../forms/Dropdown.jsx';
import { Input } from '../forms/Input.jsx';

// figma node: 9383:24495 Toolbar (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "type=" + __venc(p.type);

export function Toolbar(_p = {}) {
  _p = __slateNorm(_p, {"size":["md","lg","sm"],"type":["static","contextual (floating)"]});
  const props = { ..._p, size: _p.size ?? "md", type: _p.type ?? "static", endContent: _p.endContent ?? true, endSearch: _p.endSearch ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 1176,
      height: 40,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: 170,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 112,
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Dropdown
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            appearance={"outline"}
            size={"sm"}
            expanded={false}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{ position: "relative", width: 56, flexShrink: 0 }}>{props.icon1 ?? <Dropdown appearance={"outline"} size={"sm"} expanded={false} />}</div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      {props.endContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 8px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          justifyContent: "center",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Input
            style={{ position: "relative", flexShrink: 0 }}
            style2={"outline"}
            size={"sm"}
            state={"rest"}
          />
        </div>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 1176,
      height: 48,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: 54,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "8px 20px 8px 20px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 8px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-on-brand-rest)",
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
              padding: "0px 0px 0px 0px",
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
                  color: "var(--neutral-foreground-on-brand-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Text</span>
              </div>
              <Chevron
                style={{
                  position: "relative",
                  width: 12,
                  height: 12,
                  flexShrink: 0,
                  color: "var(--neutral-foreground-on-brand-rest-3)",
                }}
                direction={"down"}
                size={"12"}
                theme={"regular"}
              />
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}>{props.icon1 ?? <DEPRECATEDDivider layout={"default"} style2={"default"} inset={"block (default)"} contentAlign={"center (default)"} orientation={"horizontal"} />}</div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}>{props.icon2 ?? <DEPRECATEDDivider layout={"default"} style2={"default"} inset={"block (default)"} contentAlign={"center (default)"} orientation={"horizontal"} />}</div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
      </div>
      {props.endContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 20px 8px 0px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 8px 0px 8px",
          justifyContent: "center",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Input
            style={{ position: "relative", width: 200, flexShrink: 0 }}
            style2={"outline"}
            size={"medium (default)"}
            state={"rest"}
          />
        </div>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 1176,
      height: 32,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: 206,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 4px 0px 4px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 112,
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Dropdown
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            appearance={"outline"}
            size={"sm"}
            expanded={false}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{ position: "relative", width: 56, flexShrink: 0 }}>{props.icon1 ?? <Dropdown appearance={"outline"} size={"sm"} expanded={false} />}</div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      {props.endContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 4px 0px 0px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        {props.endSearch && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          justifyContent: "center",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Input
            style={{ position: "relative", flexShrink: 0 }}
            style2={"outline"}
            size={"sm"}
            state={"rest"}
          />
        </div>
        )}
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 1176,
      height: 40,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
      display: "flex",
      flexDirection: "row",
      gap: 170,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Dropdown
            style={{ position: "relative", width: 120, flexShrink: 0 }}
            appearance={"outline"}
            size={"sm"}
            expanded={false}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{ position: "relative", width: 56, flexShrink: 0 }}>{props.icon1 ?? <Dropdown appearance={"outline"} size={"sm"} expanded={false} />}</div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      {props.endContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 8px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 188,
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          justifyContent: "center",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Input
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            style2={"outline"}
            size={"sm"}
            state={"rest"}
          />
        </div>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 1176,
      height: 48,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
      display: "flex",
      flexDirection: "row",
      gap: 54,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "8px 20px 8px 20px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 8px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
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
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            }}>
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "relative",
                width: 16,
                height: 16,
                flexShrink: 0,
                color: "var(--neutral-foreground-on-brand-rest)",
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
              padding: "0px 0px 0px 0px",
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
                  color: "var(--neutral-foreground-on-brand-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>Text</span>
              </div>
              <Chevron
                style={{
                  position: "relative",
                  width: 12,
                  height: 12,
                  flexShrink: 0,
                  color: "var(--neutral-foreground-on-brand-rest-3)",
                }}
                direction={"down"}
                size={"12"}
                theme={"regular"}
              />
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}>{props.icon1 ?? <DEPRECATEDDivider layout={"default"} style2={"default"} inset={"block (default)"} contentAlign={"center (default)"} orientation={"horizontal"} />}</div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}>{props.icon2 ?? <DEPRECATEDDivider layout={"default"} style2={"default"} inset={"block (default)"} contentAlign={"center (default)"} orientation={"horizontal"} />}</div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
      </div>
      {props.endContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "8px 20px 8px 0px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 8px 0px 8px",
          justifyContent: "center",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Input
            style={{ position: "relative", width: 200, flexShrink: 0 }}
            style2={"outline"}
            size={"medium (default)"}
            state={"rest"}
          />
        </div>
      </div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 1176,
      height: 32,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
      display: "flex",
      flexDirection: "row",
      gap: 206,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 4px 0px 4px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 112,
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Dropdown
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            appearance={"outline"}
            size={"sm"}
            expanded={false}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{ position: "relative", width: 56, flexShrink: 0 }}>{props.icon1 ?? <Dropdown appearance={"outline"} size={"sm"} expanded={false} />}</div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "6px 0px 6px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDDivider
            style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 20,
              transform: "matrix(0,-1,1,0,0,26)",
              transformOrigin: "0 0",
              height: 24,
            }}
            layout={"default"}
            style2={"default"}
            inset={"block (default)"}
            contentAlign={"center (default)"}
            orientation={"horizontal"}
          />
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      {props.endContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 4px 0px 0px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
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
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
              display: "flex",
              flexDirection: "row",
              gap: 10,
              padding: "2px 2px 2px 2px",
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
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
        {props.endSearch && (
        <div style={{
          position: "relative",
          width: 188,
          display: "flex",
          flexDirection: "column",
          padding: "4px 6px 4px 6px",
          justifyContent: "center",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Input
            style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              width: "auto",
            }}
            style2={"outline"}
            size={"sm"}
            state={"rest"}
          />
        </div>
        )}
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Size=Medium, Type=Static
    "size=md|type=static": __body0,
    // figma: Size=Large, Type=Static
    "size=lg|type=static": __body1,
    // figma: Size=Small, Type=Static
    "size=sm|type=static": __body2,
    // figma: Size=Medium, Type=Contextual (floating)
    "size=md|type=contextual (floating)": __body3,
    // figma: Size=Large, Type=Contextual (floating)
    "size=lg|type=contextual (floating)": __body4,
    // figma: Size=Small, Type=Contextual (floating)
    "size=sm|type=contextual (floating)": __body5,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Toolbar;
