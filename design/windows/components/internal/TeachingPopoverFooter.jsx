import { __slateNorm } from './slateProps.jsx';
// figma node: 9322:1071 .TeachingPopover - Footer (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type);

export function TeachingPopoverFooter(_p = {}) {
  _p = __slateNorm(_p, {"type":["multi","single"]});
  const props = { ..._p, carouselSteps: _p.carouselSteps ?? true, count: _p.count ?? true, type: _p.type ?? "multi", countString: _p.countString ?? "1 of 4" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 288,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "12px 0px 12px 0px",
      justifyContent: "flex-end",
      alignItems: "flex-end",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--size-120) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 74,
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
            }}>Primary</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        minWidth: 96,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "5px 11px 5px 11px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          height: 20,
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
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Secondary</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 288,
      display: "flex",
      flexDirection: "row",
      padding: "12px 0px 12px 0px",
      justifyContent: "space-between",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 54,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "5px 12px 5px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          height: 20,
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
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>Back</span>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.carouselSteps && (
        <div style={{
          position: "relative",
          width: 40,
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
            padding: "6px 4px 6px 4px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--horizontal-xs) * 1px)",
            paddingTop: "calc(var(--vertical-snudge) * 1px)",
            paddingRight: "calc(var(--horizontal-xs) * 1px)",
            paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                width: 16,
                borderRadius: 9999,
                backgroundColor: "var(--brand-foreground-2-rest-4)",
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
            padding: "6px 4px 6px 4px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--horizontal-xs) * 1px)",
            paddingTop: "calc(var(--vertical-snudge) * 1px)",
            paddingRight: "calc(var(--horizontal-xs) * 1px)",
            paddingBottom: "calc(var(--vertical-snudge) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--brand-foreground-2-rest)",
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
            padding: "6px 4px 6px 4px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--horizontal-xs) * 1px)",
            paddingTop: "calc(var(--vertical-snudge) * 1px)",
            paddingRight: "calc(var(--horizontal-xs) * 1px)",
            paddingBottom: "calc(var(--vertical-snudge) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--brand-foreground-2-rest)",
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
            padding: "6px 4px 6px 4px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--horizontal-xs) * 1px)",
            paddingTop: "calc(var(--vertical-snudge) * 1px)",
            paddingRight: "calc(var(--horizontal-xs) * 1px)",
            paddingBottom: "calc(var(--vertical-snudge) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--brand-foreground-2-rest)",
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
            padding: "6px 4px 6px 4px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--horizontal-xs) * 1px)",
            paddingTop: "calc(var(--vertical-snudge) * 1px)",
            paddingRight: "calc(var(--horizontal-xs) * 1px)",
            paddingBottom: "calc(var(--vertical-snudge) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 8,
              opacity: 0.3,
              borderRadius: 9999,
              backgroundColor: "var(--brand-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }} />
          </div>
        </div>
        )}
        {props.count && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-3-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.countString}</span>
        )}
      </div>
      <div style={{
        position: "relative",
        width: 55,
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
            }}>Next</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Type=Single
    "type=single": __body0,
    // figma: Type=Multi
    "type=multi": __body1,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default TeachingPopoverFooter;
