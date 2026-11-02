import { TeachingPopoverFooterOnBrand } from './TeachingPopoverFooterOnBrand.jsx';

// figma node: 9322:1109 .TeachingPopover Content - On Brand
export function TeachingPopoverContentOnBrand(_p = {}) {
  const props = { ..._p, header: _p.header ?? true, bodyTitle: _p.bodyTitle ?? true, dismiss: _p.dismiss ?? true, headerTips: _p.headerTips ?? true, headerContent: _p.headerContent ?? true, body: _p.body ?? true, lastLineString: _p.lastLineString ?? "Additional body text", titleString: _p.titleString ?? "Title", footer: _p.footer ?? true, bodyString: _p.bodyString ?? "A detailed description of the content that helps the user understand or learn", inlineLink: _p.inlineLink ?? false };
  return (
    <div className={props.className} style={{
      width: 288,
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-m-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.header && (
      <div style={{
        position: "relative",
        height: 116,
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-xxs) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.headerTips && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "flex-end",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: -7,
            top: 0,
            height: 24,
            minWidth: 24,
            overflow: "hidden",
            borderRadius: 4,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            padding: "0px 4px 0px 4px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                width: 16,
                height: 16,
                overflow: "hidden",
                flexShrink: 0,
              }}>
                <svg width={9} height={12} viewBox="0 0 9 12" fill="none" style={{
                  position: "absolute",
                  left: 3.5,
                  top: 2,
                  width: 9,
                  height: 12,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 1 4.5 C 1 2.567 2.567 1 4.5 1 C 6.433 1 8 2.567 8 4.5 C 8 5.477 7.6 6.36 6.954 6.995 C 6.761 7.185 6.591 7.432 6.51 7.731 L 6.164 9 L 2.836 9 L 2.49 7.731 C 2.409 7.432 2.239 7.185 2.046 6.995 C 1.4 6.36 1 5.477 1 4.5 Z M 3.109 10 L 5.891 10 L 5.719 10.632 C 5.659 10.849 5.462 11 5.236 11 L 3.764 11 C 3.538 11 3.341 10.849 3.281 10.632 L 3.109 10 Z M 4.5 0 C 2.015 0 0 2.015 0 4.5 C 0 5.756 0.515 6.893 1.345 7.708 C 1.441 7.804 1.5 7.902 1.526 7.995 L 2.317 10.895 C 2.495 11.547 3.087 12 3.764 12 L 5.236 12 C 5.913 12 6.505 11.547 6.683 10.895 L 7.474 7.995 C 7.5 7.902 7.559 7.804 7.655 7.708 C 8.485 6.893 9 5.756 9 4.5 C 9 2.015 6.985 0 4.5 0 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
            <div style={{
              position: "relative",
              height: 16,
              display: "flex",
              flexDirection: "column",
              padding: "0px 2px 0px 2px",
              justifyContent: "flex-end",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 12,
                textAlign: "center",
                whiteSpace: "nowrap",
                lineHeight: "16px",
                color: "var(--neutral-foreground-on-brand-rest)",
                flexShrink: 0,
              }}>Tips</span>
            </div>
          </div>
          {props.dismiss && (
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
                <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
                  position: "absolute",
                  left: 2.5,
                  top: 2.5,
                  width: 11,
                  height: 11,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          )}
        </div>
        )}
        <div style={{
          position: "relative",
          backgroundColor: "var(--brand-background-2-rest)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "absolute",
            left: 61,
            top: 38,
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
      </div>
      )}
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-s-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.bodyTitle && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            lineHeight: "22px",
            color: "var(--neutral-foreground-on-brand-rest)",
            flexGrow: 1,
          }}>{props.titleString}</span>
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              lineHeight: "20px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexGrow: 1,
            }}>{props.bodyString}</span>
          </div>
          {props.inlineLink && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>{props.lastLineString}</span>
            <div style={{
              position: "relative",
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
                gap: -2,
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
                  color: "var(--neutral-foreground-inverted-link-rest)",
                  flexShrink: 0,
                }}>Always underline links</span>
                <div style={{
                  position: "relative",
                  height: 3,
                  borderTop: "1px solid var(--neutral-foreground-inverted-link-rest)",
                  borderRight: "1px solid var(--neutral-foreground-inverted-link-rest)",
                  borderBottom: "1px solid var(--neutral-foreground-inverted-link-rest)",
                  borderLeft: "1px solid var(--neutral-foreground-inverted-link-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }} />
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
                  color: "var(--neutral-foreground-inverted-link-rest)",
                }}>
                  <path d={"M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 12.105 1.895 13 3 13 L 11 13 C 12.105 13 13 12.105 13 11 L 13 8.5 C 13 8.224 13.224 8 13.5 8 C 13.776 8 14 8.224 14 8.5 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 C 0 1.343 1.343 0 3 0 L 5.5 0 C 5.776 0 6 0.224 6 0.5 C 6 0.776 5.776 1 5.5 1 L 3 1 Z M 8 0.5 C 8 0.224 8.224 0 8.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 L 14 5.5 C 14 5.776 13.776 6 13.5 6 C 13.224 6 13 5.776 13 5.5 L 13 1.707 L 8.854 5.854 C 8.658 6.049 8.342 6.049 8.146 5.854 C 7.951 5.658 7.951 5.342 8.146 5.146 L 12.293 1 L 8.5 1 C 8.224 1 8 0.776 8 0.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          )}
        </div>
      </div>
      )}
      {props.footer && (
      <TeachingPopoverFooterOnBrand
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        type={"single"}
      />
      )}
    </div>
  );
}
export default TeachingPopoverContentOnBrand;
