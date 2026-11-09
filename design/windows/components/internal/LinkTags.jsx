import { __slateNorm } from './slateProps.jsx';
import { DEPRECATEDAvatar } from './DEPRECATEDAvatar.jsx';

// figma node: 9000:4677 .link-tags (8 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "style2=" + __venc(p.style2);

export function LinkTags(_p = {}) {
  _p = __slateNorm(_p, {"type":["owner name","partner owned","view documentation","engineering assets","design explorations","divider"],"style2":["pill","text","divider"]});
  const props = { ..._p, type: _p.type ?? "owner name", style2: _p.style2 ?? "pill" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-3-rest-4)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-none) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-none) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDAvatar
            style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
            }}
            layout={"image"}
            size={"28"}
            color={"neutral"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Owner name</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 221,
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-3-rest-4)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-none) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-none) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <DEPRECATEDAvatar
            style={{
              position: "relative",
              width: 28,
              height: 28,
              flexShrink: 0,
            }}
            layout={"icon"}
            size={"28"}
            color={"brand"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Owned by [Team name]</span>
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
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-3-rest-4)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-none) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-none) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
          }}>
            <svg width={15.996} height={15.999} viewBox="0 0 15.996 15.999" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 15.996,
              height: 15.999,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 1 2 C 1 0.895 1.895 0 3 0 L 10 0 C 11.105 0 12 0.895 12 2 L 12 3.896 L 11 5.086 L 11 2 C 11 1.448 10.552 1 10 1 L 3 1 C 2.448 1 2 1.448 2 2 L 2.5 2 C 2.776 2 3 2.224 3 2.5 C 3 2.776 2.776 3 2.5 3 L 2 3 L 2 6 L 2.5 6 C 2.776 6 3 6.224 3 6.5 C 3 6.776 2.776 7 2.5 7 L 2 7 L 2 10 L 2.5 10 C 2.776 10 3 10.224 3 10.5 C 3 10.776 2.776 11 2.5 11 L 2 11 C 2 11.552 2.448 12 3 12 L 5.194 12 L 4.354 13 L 3 13 C 1.895 13 1 12.105 1 11 L 0.5 11 C 0.224 11 0 10.776 0 10.5 C 0 10.224 0.224 10 0.5 10 L 1 10 L 1 7 L 0.5 7 C 0.224 7 0 6.776 0 6.5 C 0 6.224 0.224 6 0.5 6 L 1 6 L 1 3 L 0.5 3 C 0.224 3 0 2.776 0 2.5 C 0 2.224 0.224 2 0.5 2 L 1 2 Z M 13.003 9.61 C 13.003 9.376 12.712 9.27 12.562 9.449 L 9.923 12.594 C 9.786 12.756 9.902 13.004 10.114 13.004 L 12.251 13.004 C 12.665 13.004 13 12.669 13.001 12.255 L 13.003 9.61 Z M 15.996 2.751 C 15.996 2.051 15.122 1.733 14.672 2.269 L 4.176 14.767 C 3.766 15.255 4.113 15.999 4.75 15.999 L 13.996 15.999 C 15.101 15.999 15.996 15.104 15.996 13.999 L 15.996 2.751 Z M 14.996 3.438 L 14.996 13.999 C 14.996 14.552 14.548 14.999 13.996 14.999 L 5.287 14.999 L 14.996 3.438 Z M 4 4.5 C 4 4.224 4.224 4 4.5 4 L 8.5 4 C 8.776 4 9 4.224 9 4.5 C 9 4.776 8.776 5 8.5 5 L 4.5 5 C 4.224 5 4 4.776 4 4.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>View documentation</span>
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
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-3-rest-4)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-none) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-none) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={16} height={14.000} viewBox="0 0 16 14.000" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 3,
              width: 16,
              height: 14,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 10.967 0.68 C 11.066 0.422 10.937 0.133 10.68 0.033 C 10.422 -0.066 10.132 0.063 10.033 0.321 L 5.033 13.321 C 4.934 13.578 5.063 13.868 5.321 13.967 C 5.578 14.066 5.868 13.937 5.967 13.68 L 10.967 0.68 Z M 3.825 3.121 C 4.035 3.3 4.059 3.616 3.88 3.826 L 1.159 7 L 3.88 10.175 C 4.059 10.384 4.035 10.7 3.825 10.88 C 3.616 11.059 3.3 11.035 3.12 10.826 L 0.12 7.326 C -0.04 7.138 -0.04 6.862 0.12 6.675 L 3.12 3.175 C 3.3 2.965 3.616 2.941 3.825 3.121 Z M 12.175 11.38 C 11.965 11.2 11.941 10.884 12.12 10.675 L 14.841 7.5 L 12.12 4.326 C 11.941 4.116 11.965 3.8 12.175 3.62 C 12.384 3.441 12.7 3.465 12.88 3.675 L 15.88 7.175 C 16.04 7.362 16.04 7.638 15.88 7.826 L 12.88 11.326 C 12.7 11.535 12.384 11.559 12.175 11.38 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Engineering assets</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-3-rest-4)",
        boxShadow: "inset 0 0 0 1px rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-none) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-none) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={18.714} height={28} viewBox="0 0 18.714 28" fill="none" style={{
              position: "absolute",
              left: 6.643,
              top: 2,
              width: 18.714,
              height: 28,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 5.179 0 L 13.535 0 C 16.428 0 18.714 2.424 18.714 5.334 C 18.714 7.103 17.869 8.693 16.556 9.666 C 17.869 10.641 18.714 12.231 18.714 14 C 18.714 16.91 16.428 19.334 13.535 19.334 C 12.33 19.334 11.231 18.913 10.358 18.212 L 10.358 22.666 C 10.358 25.576 8.072 28 5.179 28 C 2.286 28 0 25.576 0 22.666 C 0 20.897 0.845 19.307 2.158 18.333 C 0.845 17.359 0 15.769 0 14 C 0 12.231 0.845 10.641 2.158 9.666 C 0.845 8.693 0 7.103 0 5.334 C 0 2.424 2.286 0 5.179 0 Z M 2 5.334 C 2 7.208 3.458 8.666 5.179 8.666 L 8.356 8.666 L 8.356 2 L 5.179 2 C 3.459 2 2 3.459 2 5.334 Z M 8.358 13.885 L 8.358 10.666 L 5.179 10.666 C 3.459 10.666 2 12.126 2 14 C 2 15.874 3.459 17.333 5.179 17.333 L 8.358 17.333 L 8.358 14.115 C 8.357 14.077 8.356 14.039 8.356 14 C 8.356 13.961 8.357 13.923 8.358 13.885 Z M 10.358 14.093 C 10.405 15.922 11.844 17.334 13.535 17.334 C 15.256 17.334 16.714 15.874 16.714 14 C 16.714 12.126 15.256 10.666 13.535 10.666 C 11.844 10.666 10.405 12.078 10.358 13.907 L 10.358 14.093 Z M 13.535 8.666 C 15.256 8.666 16.714 7.208 16.714 5.334 C 16.714 3.459 15.256 2 13.535 2 L 10.358 2 L 10.358 8.666 L 13.535 8.666 Z M 5.179 19.333 C 3.459 19.333 2 20.792 2 22.666 C 2 24.541 3.459 26 5.179 26 C 6.899 26 8.358 24.541 8.358 22.666 L 8.358 19.333 L 5.179 19.333 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest-3)",
              flexShrink: 0,
            }}>Design explorations</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 144,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 144,
        height: 24,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--neutral-foreground-1-rest-3)",
      }}>{props.text1 ?? "View documentation"}</span>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: 10,
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Engineering assets"}</span>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--neutral-foreground-disabled-rest-4)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        height: 31,
        overflow: "hidden",
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 7.5,
          top: 3.5,
          width: 1,
          height: 24,
          borderRadius: 0.5,
          backgroundColor: "currentColor",
        }} />
      </div>
    </div>
  );
  const __impls = {
    // figma: Type=Owner name, Style=Pill
    "type=owner name|style2=pill": __body0,
    // figma: Type=Partner owned, Style=Pill
    "type=partner owned|style2=pill": __body1,
    // figma: Type=View documentation, Style=Pill
    "type=view documentation|style2=pill": __body2,
    // figma: Type=Engineering assets, Style=Pill
    "type=engineering assets|style2=pill": __body3,
    // figma: Type=Design explorations, Style=Pill
    "type=design explorations|style2=pill": __body4,
    // figma: Type=View documentation, Style=Text
    "type=view documentation|style2=text": __body5,
    // figma: Type=Engineering assets, Style=Text
    "type=engineering assets|style2=text": __body6,
    // figma: Type=Divider, Style=Divider
    "type=divider|style2=divider": __body7,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default LinkTags;
