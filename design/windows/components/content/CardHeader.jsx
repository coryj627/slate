import { ArrowSwap2 } from '../internal/ArrowSwap2.jsx';
import { ImageCircle2 } from '../internal/ImageCircle2.jsx';

// figma node: 9230:4878 .Card header (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "customHeader=" + __venc(p.customHeader);

export function CardHeader(_p = {}) {
  const props = { ..._p, customHeader: _p.customHeader ?? false, checkbox: _p.checkbox ?? false, avatar: _p.avatar ?? false, headerQuickAction: _p.headerQuickAction ?? true, subtextString: _p.subtextString ?? "Additional metadata", productIcon: _p.productIcon ?? true, titleString: _p.titleString ?? "Card title", subtext: _p.subtext ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.avatar && (
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
      )}
      {props.productIcon && (
      <div style={{
          position: "relative",
          width: 32,
          height: 32,
          flexShrink: 0,
          color: "var(--neutral-foreground-1-rest)",
        }}>{props.pxIcon32 ?? <ImageCircle2 size={"32"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexGrow: 1,
        alignSelf: "stretch",
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
        }}>{props.titleString}</span>
        {props.subtext && (
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
        }}>{props.subtextString}</span>
        )}
      </div>
      {props.checkbox && (
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
      )}
      {props.headerQuickAction && (
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
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 36,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        backgroundColor: "var(--brand-background-2-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ArrowSwap2
            style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}
            size={"16"}
            theme={"regular"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 0px 2px 0px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              textAlign: "center",
              lineHeight: "14px",
              color: "var(--brand-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>SWAP AREA WITH YOUR COMPONENT</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Custom header=False
    "customHeader=false": __body0,
    // figma: Custom header=True
    "customHeader=true": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default CardHeader;
