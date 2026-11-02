import { Divider } from '../content/Divider.jsx';

// figma node: 9170:8121 .Drawer footer
export function DrawerFooter(_p = {}) {
  const props = { ..._p, primaryButton: _p.primaryButton ?? true, moreButton: _p.moreButton ?? true, divider: _p.divider ?? false, secondaryButton: _p.secondaryButton ?? true };
  return (
    <div className={props.className} style={{
      width: 592,
      borderRadius: 24,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      padding: "16px 24px 24px 24px",
      justifyContent: "flex-end",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.primaryButton && (
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
              }}>{props.primaryLabel ?? "Primary"}</span>
            </div>
          </div>
        </div>
        )}
        {props.secondaryButton && (
        <div style={{
          position: "relative",
          width: 91,
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
              }}>{props.secondaryLabel ?? "Secondary"}</span>
            </div>
          </div>
        </div>
        )}
        {props.moreButton && (
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
      {props.divider && (
      <Divider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 592,
          height: 1,
        }}
        contentSlot={false}
        layout={"center (default)"}
        vertical={false}
      />
      )}
    </div>
  );
}
export default DrawerFooter;
