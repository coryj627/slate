import { __slateNorm } from '../internal/slateProps.jsx';
import { FocusRing } from '../internal/FocusRing.jsx';

// figma node: 9249:10255 .Content header (4 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "style2=" + __venc(p.style2) + '|' + "sorting=" + __venc(p.sorting);

export function ContentHeader(_p = {}) {
  _p = __slateNorm(_p, {"layout":["selection","content"],"style2":["regular","semibold"]});
  const props = { ..._p, layout: _p.layout ?? "selection", style2: _p.style2 ?? "regular", sorting: _p.sorting ?? false, focus: _p.focus ?? false, headerContent: _p.headerContent ?? "Header" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 200,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "5px 8px 6px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
          color: "var(--header-text)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.headerContent}</span>
      </div>
      {props.focus && (
      <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 200,
          height: 33,
        }}>
        <FocusRing style={{ transform: "scale(1.299, 0.344)", transformOrigin: "0 0" }} />
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 200,
      height: 33,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-2-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
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
        width: 250,
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "5px 8px 6px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
          color: "var(--header-text)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.headerContent}</span>
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
              <svg width={8} height={9} viewBox="0 0 8 9" fill="none" style={{
                position: "absolute",
                left: 2,
                top: 1.5,
                width: 8,
                height: 9,
                color: "var(--neutral-foreground-1-rest)",
              }}>
                <path d={"M 4 0 C 4.276 0 4.5 0.224 4.5 0.5 L 4.5 7.293 L 7.146 4.646 C 7.342 4.451 7.658 4.451 7.854 4.646 C 8.049 4.842 8.049 5.158 7.854 5.354 L 4.354 8.854 C 4.158 9.049 3.842 9.049 3.646 8.854 L 0.146 5.354 C -0.049 5.158 -0.049 4.842 0.146 4.646 C 0.342 4.451 0.658 4.451 0.854 4.646 L 3.5 7.293 L 3.5 0.5 C 3.5 0.224 3.724 0 4 0 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      {props.focus && (
      <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 200,
          height: 33,
        }}>
        <FocusRing style={{ transform: "scale(1.299, 0.344)", transformOrigin: "0 0" }} />
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 200,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-2-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
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
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "5px 8px 6px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
          color: "var(--header-text)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.headerContent}</span>
      </div>
      {props.focus && (
      <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 200,
          height: 33,
        }}>
        <FocusRing style={{ transform: "scale(1.299, 0.344)", transformOrigin: "0 0" }} />
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-2-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 9px 16px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: -8,
          top: -8,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
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
      </div>
      {props.focus && (
      <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 33,
        }}>{props.icon2 ?? <FocusRing style={{ transform: "scale(0.260, 0.344)", transformOrigin: "0 0" }} />}</div>
      )}
    </div>
  );
  const __impls = {
    // figma: Layout=Content, Style=Regular, Sorting=False
    "layout=content|style2=regular|sorting=false": __body0,
    // figma: Layout=Content, Style=Semibold, Sorting=True
    "layout=content|style2=semibold|sorting=true": __body1,
    // figma: Layout=Content, Style=Semibold, Sorting=False
    "layout=content|style2=semibold|sorting=false": __body2,
    // figma: Layout=Selection, Style=Regular, Sorting=False
    "layout=selection|style2=regular|sorting=false": __body3,
  };
  return (__impls[__vkey(props)] ?? __body3)();
}
export default ContentHeader;
