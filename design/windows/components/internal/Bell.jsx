import { __slateNorm } from './slateProps.jsx';
import { Button3 } from './Button3.jsx';
import { Icon24Close } from './Icon24Close.jsx';
import { Icon24PlaceholderSmall } from './Icon24PlaceholderSmall.jsx';

// figma node: 9738:813 Bell (4 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "variant=" + __venc(p.variant) + '|' + "state=" + __venc(p.state);

export function Bell(_p = {}) {
  _p = __slateNorm(_p, {"variant":["default","message","message w dismiss"],"state":["default","danger"]});
  const props = { ..._p, variant: _p.variant ?? "default", icon: _p.icon ?? true, close: _p.close ?? true, cTA2: _p.cTA2 ?? true, state: _p.state ?? "default", cTA1: _p.cTA1 ?? true, count: _p.count ?? false, cTARail: _p.cTARail ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      overflow: "hidden",
      borderRadius: 13,
      backgroundColor: "rgb(44,44,44)",
      boxShadow: "0px 0px 0.500px 0px rgba(0,0,0,0.3), 0px 1px 3px 0px rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-2) * 1px)",
      padding: "0px 8px 0px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacers-spacer-0) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
          }}>{props.icon2 ?? <Icon24PlaceholderSmall />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacers-spacer-2) * 1px)",
            padding: "0px 4px 0px 4px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-1) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgb(255,255,255)",
              flexShrink: 0,
            }}>{props.text1 ?? "Default messaging bell"}</span>
            {props.count && (
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 500,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgba(255,255,255,0.7)",
              flexShrink: 0,
            }}>{props.text2 ?? "1/134"}</span>
            )}
          </div>
          {props.cTARail && (
          <div style={{
            position: "relative",
            width: 153,
            display: "flex",
            flexDirection: "row",
            gap: 8,
            padding: "0px 4px 0px 4px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-0) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            {props.cTA1 && (
            <Button3
              style={{ position: "relative", height: 24, flexShrink: 0 }}
              label={"Action"}
              variant={"secondary"}
              iconLead={"false"}
              size={"default"}
              disabled={false}
              state={"default"}
            />
            )}
            {props.cTA2 && (
            <Button3
              style={{ position: "relative", height: 24, flexShrink: 0 }}
              label={"Action"}
              variant={"secondary"}
              iconLead={"false"}
              size={"default"}
              disabled={false}
              state={"default"}
            />
            )}
            {props.close && (
            <div style={{
              position: "relative",
              width: 33,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 1,
                height: 40,
                backgroundColor: "rgb(68,68,68)",
              }} />
              <Icon24Close style={{
                  position: "absolute",
                  left: 9,
                  top: 8,
                  width: 24,
                  height: 24,
                }} />
            </div>
            )}
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      overflow: "hidden",
      borderRadius: 13,
      backgroundColor: "rgb(44,44,44)",
      boxShadow: "0px 0px 0.500px 0px rgba(0,0,0,0.3), 0px 1px 3px 0px rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-2-2) * 1px)",
      padding: "0px 8px 0px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2-2) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacers-spacer-0-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacers-spacer-2) * 1px)",
            padding: "0px 8px 0px 8px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgb(255,255,255)",
              flexShrink: 0,
            }}>{props.text1 ?? "Message only"}</span>
            {props.count && (
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 500,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgba(255,255,255,0.7)",
              flexShrink: 0,
            }}>{props.text2 ?? "1/134"}</span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      overflow: "hidden",
      borderRadius: 13,
      backgroundColor: "rgb(44,44,44)",
      boxShadow: "0px 0px 0.500px 0px rgba(0,0,0,0.3), 0px 1px 3px 0px rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-2-2) * 1px)",
      padding: "0px 8px 0px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2-2) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacers-spacer-0-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacers-spacer-2) * 1px)",
            padding: "0px 4px 0px 4px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-1) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgb(255,255,255)",
              flexShrink: 0,
            }}>{props.text1 ?? "Message with explicit dismiss"}</span>
            {props.count && (
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 500,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgba(255,255,255,0.7)",
              flexShrink: 0,
            }}>{props.text2 ?? "1/134"}</span>
            )}
          </div>
          {props.cTARail && (
          <div style={{
            position: "relative",
            width: 37,
            height: 24,
            display: "flex",
            flexDirection: "row",
            gap: 8,
            padding: "0px 4px 0px 4px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-0) * 1px)",
            flexShrink: 0,
          }}>
            {props.close && (
            <div style={{
              position: "relative",
              width: 33,
              height: 40,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 1,
                height: 40,
                backgroundColor: "rgb(68,68,68)",
              }} />
              <Icon24Close style={{
                  position: "absolute",
                  left: 9,
                  top: 8,
                  width: 24,
                  height: 24,
                }} />
            </div>
            )}
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 40,
      overflow: "hidden",
      borderRadius: 13,
      backgroundColor: "var(--bg-bg-danger)",
      boxShadow: "0px 0px 0.500px 0px rgba(0,0,0,0.3), 0px 1px 3px 0px rgba(0,0,0,0.15)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacers-spacer-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
      paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacers-spacer-0) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 24,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--icon-icon-onbrand)",
          }}>{props.icon2 ?? <Icon24PlaceholderSmall />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacers-spacer-2) * 1px)",
            padding: "0px 4px 0px 4px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-1) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "var(--text-danger-ondanger)",
              flexShrink: 0,
            }}>{props.text1 ?? "Danger messaging bell"}</span>
            {props.count && (
            <span style={{
              position: "relative",
              fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 500,
              fontSize: 11,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              letterSpacing: "0.005em",
              color: "rgba(0,0,0,0.5)",
              flexShrink: 0,
            }}>{props.text2 ?? "1/134"}</span>
            )}
          </div>
          {props.cTARail && (
          <div style={{
            position: "relative",
            width: 153,
            display: "flex",
            flexDirection: "row",
            gap: 8,
            padding: "0px 4px 0px 4px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacers-spacer-1) * 1px)",
            paddingRight: "calc(var(--spacers-spacer-0) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            {props.cTA1 && (
            <div style={{
              position: "relative",
              width: 50,
              borderRadius: 5,
              boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
              display: "flex",
              flexDirection: "row",
              padding: "4px 8px 4px 8px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
              paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
              paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
              paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "relative",
                width: 24,
                height: 24,
                flexShrink: 0,
              }}>
                <svg width={16.501} height={15.626} viewBox="0 0 16.501 15.626" fill="none" style={{
                  position: "absolute",
                  left: 3.749,
                  top: 3.917,
                  width: 16.501,
                  height: 15.626,
                  color: "rgba(0,0,0,0.9)",
                }}>
                  <path d={"M 9.893 6.084 L 9.664 5.403 L 8.251 1.213 L 6.837 5.403 L 6.608 6.084 L 5.89 6.084 L 1.196 6.084 L 5.04 9.033 L 5.601 9.463 L 5.382 10.136 L 3.975 14.459 L 7.662 11.775 L 8.251 11.347 L 8.839 11.775 L 12.526 14.459 L 11.119 10.136 L 10.901 9.463 L 11.461 9.033 L 15.305 6.084 L 10.611 6.084 L 9.893 6.084 Z M 5.89 5.084 L 0.902 5.084 C 0.042 5.084 -0.328 6.174 0.354 6.698 L 4.431 9.826 L 2.929 14.443 C 2.659 15.273 3.609 15.963 4.315 15.449 L 8.251 12.584 L 12.187 15.449 C 12.892 15.963 13.842 15.273 13.572 14.443 L 12.07 9.826 L 16.147 6.698 C 16.829 6.174 16.459 5.084 15.599 5.084 L 10.611 5.084 L 9.103 0.612 C 8.828 -0.204 7.673 -0.204 7.398 0.612 L 5.89 5.084 Z"} fill="currentColor" fillRule="evenodd" />
                </svg>
              </div>
              <span style={{
                position: "relative",
                fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 500,
                fontSize: 11,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "16px",
                letterSpacing: "0.005em",
                color: "var(--text-danger-ondanger)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Action</span>
            </div>
            )}
            {props.cTA2 && (
            <div style={{
              position: "relative",
              width: 50,
              borderRadius: 5,
              boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.1)",
              display: "flex",
              flexDirection: "row",
              padding: "4px 8px 4px 8px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacers-spacer-2) * 1px)",
              paddingTop: "calc(var(--spacers-spacer-1) * 1px)",
              paddingRight: "calc(var(--spacers-spacer-2) * 1px)",
              paddingBottom: "calc(var(--spacers-spacer-1) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "relative",
                width: 24,
                height: 24,
                flexShrink: 0,
              }}>
                <svg width={16.501} height={15.626} viewBox="0 0 16.501 15.626" fill="none" style={{
                  position: "absolute",
                  left: 3.749,
                  top: 3.917,
                  width: 16.501,
                  height: 15.626,
                  color: "rgba(0,0,0,0.9)",
                }}>
                  <path d={"M 9.893 6.084 L 9.664 5.403 L 8.251 1.213 L 6.837 5.403 L 6.608 6.084 L 5.89 6.084 L 1.196 6.084 L 5.04 9.033 L 5.601 9.463 L 5.382 10.136 L 3.975 14.459 L 7.662 11.775 L 8.251 11.347 L 8.839 11.775 L 12.526 14.459 L 11.119 10.136 L 10.901 9.463 L 11.461 9.033 L 15.305 6.084 L 10.611 6.084 L 9.893 6.084 Z M 5.89 5.084 L 0.902 5.084 C 0.042 5.084 -0.328 6.174 0.354 6.698 L 4.431 9.826 L 2.929 14.443 C 2.659 15.273 3.609 15.963 4.315 15.449 L 8.251 12.584 L 12.187 15.449 C 12.892 15.963 13.842 15.273 13.572 14.443 L 12.07 9.826 L 16.147 6.698 C 16.829 6.174 16.459 5.084 15.599 5.084 L 10.611 5.084 L 9.103 0.612 C 8.828 -0.204 7.673 -0.204 7.398 0.612 L 5.89 5.084 Z"} fill="currentColor" fillRule="evenodd" />
                </svg>
              </div>
              <span style={{
                position: "relative",
                fontFamily: "Inter, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 500,
                fontSize: 11,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "16px",
                letterSpacing: "0.005em",
                color: "var(--text-danger-ondanger)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Action</span>
            </div>
            )}
            {props.close && (
            <div style={{
              position: "relative",
              width: 33,
              height: 40,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 1,
                height: 40,
                backgroundColor: "rgba(0,0,0,0.1)",
              }} />
              <Icon24Close style={{
                  position: "absolute",
                  left: 9,
                  top: 8,
                  width: 24,
                  height: 24,
                  color: "var(--icon-danger-ondanger)",
                }} />
            </div>
            )}
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: 👥 Variant=Default, 🐣 State=Default
    "variant=default|state=default": __body0,
    // figma: 👥 Variant=Message, 🐣 State=Default
    "variant=message|state=default": __body1,
    // figma: 👥 Variant=Message w Dismiss, 🐣 State=Default
    "variant=message w dismiss|state=default": __body2,
    // figma: 👥 Variant=Default, 🐣 State=Danger
    "variant=default|state=danger": __body3,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Bell;
