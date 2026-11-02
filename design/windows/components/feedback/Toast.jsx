import { Dismiss3 } from '../internal/Dismiss3.jsx';
import { Info2 } from '../internal/Info2.jsx';

// figma node: 9116:16062 Toast
export function Toast(_p = {}) {
  const props = { ..._p, titleString: _p.titleString ?? "Primary information", endContent: _p.endContent ?? true, timestampString: _p.timestampString ?? "00:00 PM", additionalContent: _p.additionalContent ?? false, quickActions: _p.quickActions ?? true, additionalAction: _p.additionalAction ?? false, secondaryString: _p.secondaryString ?? "Secondary information", tertiaryInformation: _p.tertiaryInformation ?? false, progressString: _p.progressString ?? "00% complete", tertiaryString: _p.tertiaryString ?? "Tertiary information", progressBar: _p.progressBar ?? false };
  return (
    <div className={props.className} style={{
      width: 292,
      minWidth: 292,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      padding: "12px 12px 12px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-m-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-3-rest)",
              }}>{props.icon1 ?? <Info2 size={"20"} theme={"filled"} />}</div>
          </div>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
          }}>{props.titleString}</span>
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon2 ?? <Dismiss3 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.additionalContent && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 28px 0px 28px",
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
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        {props.tertiaryInformation && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.tertiaryString}</span>
        )}
        {props.progressBar && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-vertical-s-2) * 1px)",
          padding: "8px 0px 8px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 2,
            borderRadius: 9999,
            backgroundColor: "var(--neutral-background-6-rest)",
            display: "flex",
            flexDirection: "row",
            padding: "0px 100px 0px 0px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              borderRadius: 9999,
              backgroundColor: "var(--brand-background-compound-rest)",
              flexGrow: 1,
              alignSelf: "stretch",
            }} />
          </div>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.progressString}</span>
        </div>
        )}
        {props.quickActions && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
          padding: "7px 0px 7px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 40,
            height: 21,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              alignItems: "flex-start",
              flexWrap: "nowrap",
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
                color: "var(--brand-foreground-link-rest)",
                flexShrink: 0,
              }}>Action</span>
            </div>
            <div style={{
              position: "relative",
              width: 20,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                color: "var(--brand-foreground-link-rest)",
              }}>
                <path d={"M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 12.105 1.895 13 3 13 L 11 13 C 12.105 13 13 12.105 13 11 L 13 8.5 C 13 8.224 13.224 8 13.5 8 C 13.776 8 14 8.224 14 8.5 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 C 0 1.343 1.343 0 3 0 L 5.5 0 C 5.776 0 6 0.224 6 0.5 C 6 0.776 5.776 1 5.5 1 L 3 1 Z M 8 0.5 C 8 0.224 8.224 0 8.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 L 14 5.5 C 14 5.776 13.776 6 13.5 6 C 13.224 6 13 5.776 13 5.5 L 13 1.707 L 8.854 5.854 C 8.658 6.049 8.342 6.049 8.146 5.854 C 7.951 5.658 7.951 5.342 8.146 5.146 L 12.293 1 L 8.5 1 C 8.224 1 8 0.776 8 0.5 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          {props.additionalAction && (
          <div style={{
            position: "relative",
            width: 25,
            height: 21,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              alignItems: "flex-start",
              flexWrap: "nowrap",
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
                color: "var(--brand-foreground-link-rest)",
                flexShrink: 0,
              }}>Action</span>
            </div>
            <div style={{
              position: "relative",
              width: 20,
              overflow: "hidden",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                color: "var(--brand-foreground-link-rest)",
              }}>
                <path d={"M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 12.105 1.895 13 3 13 L 11 13 C 12.105 13 13 12.105 13 11 L 13 8.5 C 13 8.224 13.224 8 13.5 8 C 13.776 8 14 8.224 14 8.5 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 C 0 1.343 1.343 0 3 0 L 5.5 0 C 5.776 0 6 0.224 6 0.5 C 6 0.776 5.776 1 5.5 1 L 3 1 Z M 8 0.5 C 8 0.224 8.224 0 8.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 L 14 5.5 C 14 5.776 13.776 6 13.5 6 C 13.224 6 13 5.776 13 5.5 L 13 1.707 L 8.854 5.854 C 8.658 6.049 8.342 6.049 8.146 5.854 C 7.951 5.658 7.951 5.342 8.146 5.146 L 12.293 1 L 8.5 1 C 8.224 1 8 0.776 8 0.5 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
        </div>
        )}
      </div>
      )}
    </div>
  );
}
export default Toast;
