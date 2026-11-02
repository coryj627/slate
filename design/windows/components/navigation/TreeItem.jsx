import { __slateNorm } from '../internal/slateProps.jsx';
import { ImageCircle3 } from '../internal/ImageCircle3.jsx';
import { Placeholder4 } from '../internal/Placeholder4.jsx';

// figma node: 9064:1572 Tree item (60 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state) + '|' + "leafNode=" + __venc(p.leafNode);

export function TreeItem(_p = {}) {
  _p = __slateNorm(_p, {"style2":["subtle (default)","subtle alpha","transparent"],"size":["sm","medium (default)"],"state":["rest","hover","pressed","selected","disabled"]});
  const props = { ..._p, iconBefore: _p.iconBefore ?? true, chevron: _p.chevron ?? true, checkbox: _p.checkbox ?? false, primaryText: _p.primaryText ?? "Item title", iconAfter: _p.iconAfter ?? false, badges: _p.badges ?? false, quickActions: _p.quickActions ?? true, focus: _p.focus ?? false, style2: _p.style2 ?? "subtle (default)", size: _p.size ?? "sm", state: _p.state ?? "rest", leafNode: _p.leafNode ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={32} viewBox="0 0 280 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 28 L 280 28 L 281 28 L 281 4 L 280 4 Z M 276 32 L 276 31 L 4 31 L 4 32 L 4 33 L 276 33 L 276 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 280 28 L 279 28 C 279 29.657 277.657 31 276 31 L 276 32 L 276 33 C 278.761 33 281 30.761 281 28 L 280 28 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
            borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
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
              <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
                position: "relative",
                width: 8,
                height: 8,
                flexShrink: 0,
                color: "var(--brand-foreground-1-rest)",
              }}>
                <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--brand-foreground-1-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={32} viewBox="0 0 280 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 28 L 280 28 L 281 28 L 281 4 L 280 4 Z M 276 32 L 276 31 L 4 31 L 4 32 L 4 33 L 276 33 L 276 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 280 28 L 279 28 C 279 29.657 277.657 31 276 31 L 276 32 L 276 33 C 278.761 33 281 30.761 281 28 L 280 28 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
            borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={32} viewBox="0 0 280 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 28 L 280 28 L 281 28 L 281 4 L 280 4 Z M 276 32 L 276 31 L 4 31 L 4 32 L 4 33 L 276 33 L 276 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 280 28 L 279 28 C 279 29.657 277.657 31 276 31 L 276 32 L 276 33 C 278.761 33 281 30.761 281 28 L 280 28 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
            borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-hover)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.7)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-hover)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-hover)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.7)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-pressed)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.5)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-pressed)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-pressed)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.5)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-selected)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled20px ?? <ImageCircle3 size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled20px ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-selected)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled20px ?? <ImageCircle3 size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled20px ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled20px ?? <ImageCircle3 size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled20px ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled20px ?? <ImageCircle3 size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled20px ?? <Placeholder4 size={"20"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "8px 4px 8px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--horizontal-none) * 1px)",
        paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
        paddingTop: "calc(var(--vertical-mnudge) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 32,
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "8px 4px 8px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular20px ?? <ImageCircle3 size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular20px ?? <Placeholder4 size={"20"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={24} viewBox="0 0 280 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 20 L 280 20 L 281 20 L 281 4 L 280 4 Z M 276 24 L 276 23 L 4 23 L 4 24 L 4 25 L 276 25 L 276 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 280 20 L 279 20 C 279 21.657 277.657 23 276 23 L 276 24 L 276 25 C 278.761 25 281 22.761 281 20 L 280 20 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={24} viewBox="0 0 280 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 20 L 280 20 L 281 20 L 281 4 L 280 4 Z M 276 24 L 276 23 L 4 23 L 4 24 L 4 25 L 276 25 L 276 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 280 20 L 279 20 C 279 21.657 277.657 23 276 23 L 276 24 L 276 25 C 278.761 25 281 22.761 281 20 L 280 20 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-hover)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.7)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-hover)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-hover)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.7)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-hover)",
            borderRight: "1px solid var(--neutral-stroke-accessible-hover)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-hover)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-hover)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
      {props.quickActions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
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
                color: "var(--neutral-foreground-2-hover)",
              }}>
                <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
              </svg>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-pressed)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.5)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-pressed)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-pressed)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0.5)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderRight: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderBottom: "1px solid var(--neutral-stroke-accessible-pressed)",
            borderLeft: "1px solid var(--neutral-stroke-accessible-pressed)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-pressed)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-selected)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled16px ?? <ImageCircle3 size={"16"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled16px ?? <Placeholder4 size={"16"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-3-selected)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled16px ?? <ImageCircle3 size={"16"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled16px ?? <Placeholder4 size={"16"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled16px ?? <ImageCircle3 size={"16"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled16px ?? <Placeholder4 size={"16"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-selected)",
          }}>{props.iconBeforeFilled16px ?? <ImageCircle3 size={"16"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.iconAfter1Filled16px ?? <Placeholder4 size={"16"} theme={"filled"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--brand-background-1-rest)",
          borderTop: "1px solid var(--brand-stroke-1-rest)",
          borderRight: "1px solid var(--brand-stroke-1-rest)",
          borderBottom: "1px solid var(--brand-stroke-1-rest)",
          borderLeft: "1px solid var(--brand-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "var(--neutral-foreground-on-brand-rest)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--tree-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={24} viewBox="0 0 280 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 20 L 280 20 L 281 20 L 281 4 L 280 4 Z M 276 24 L 276 23 L 4 23 L 4 24 L 4 25 L 276 25 L 276 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 280 20 L 279 20 C 279 21.657 277.657 23 276 23 L 276 24 L 276 25 C 278.761 25 281 22.761 281 20 L 280 20 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.chevron && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "10px 6px 10px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
            position: "absolute",
            left: 4.5,
            top: 2,
            width: 4.5,
            height: 8,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: 280,
      minHeight: 24,
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 8px 0px 24px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--treeleaf-padding-left) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.state === "selected" && <span aria-hidden="true" style={{ position: "absolute", left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: "var(--accent-indicator)" }} />}
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 280,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={280} height={24} viewBox="0 0 280 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 280,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 276 1 L 276 0 L 276 -1 L 4 -1 L 4 0 Z M 280 4 L 279 4 L 279 20 L 280 20 L 281 20 L 281 4 L 280 4 Z M 276 24 L 276 23 L 4 23 L 4 24 L 4 25 L 276 25 L 276 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 280 20 L 279 20 C 279 21.657 277.657 23 276 23 L 276 24 L 276 25 C 278.761 25 281 22.761 281 20 L 280 20 Z M 276 0 L 276 1 C 277.657 1 279 2.343 279 4 L 280 4 L 281 4 C 281 1.239 278.761 -1 276 -1 L 276 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.checkbox && (
      <div style={{
        position: "relative",
        width: 24,
        maxWidth: 24,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            borderRadius: 2,
            borderTop: "1px solid var(--neutral-foreground-disabled-rest)",
            borderRight: "1px solid var(--neutral-foreground-disabled-rest)",
            borderBottom: "1px solid var(--neutral-foreground-disabled-rest)",
            borderLeft: "1px solid var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
          }} />
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.iconBefore && (
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.iconBeforeRegular16px ?? <ImageCircle3 size={"16"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryText}</span>
        </div>
        {props.iconAfter && (
        <div style={{
          position: "relative",
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
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.iconAfter1Regular16px ?? <Placeholder4 size={"16"} theme={"regular"} />}</div>
        </div>
        )}
      </div>
      {props.badges && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "4px 4px 4px 4px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          minWidth: 16,
          overflow: "hidden",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-disabled-rest)",
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 2px 0.500px 2px",
            justifyContent: "flex-end",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 10,
              textAlign: "center",
              whiteSpace: "nowrap",
              lineHeight: "14px",
              color: "rgba(255,255,255,0.4)",
              flexShrink: 0,
            }}>1</span>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Rest, Leaf node=False
    "style2=subtle (default)|size=medium (default)|state=rest|leafNode=false": __body0,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Rest, Leaf node=False
    "style2=subtle alpha|size=medium (default)|state=rest|leafNode=false": __body1,
    // figma: Style=Transparent, Size=Medium (Default), State=Rest, Leaf node=False
    "style2=transparent|size=medium (default)|state=rest|leafNode=false": __body1,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Rest, Leaf node=True
    "style2=subtle (default)|size=medium (default)|state=rest|leafNode=true": __body2,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Rest, Leaf node=True
    "style2=subtle alpha|size=medium (default)|state=rest|leafNode=true": __body2,
    // figma: Style=Transparent, Size=Medium (Default), State=Rest, Leaf node=True
    "style2=transparent|size=medium (default)|state=rest|leafNode=true": __body2,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Hover, Leaf node=False
    "style2=subtle (default)|size=medium (default)|state=hover|leafNode=false": __body3,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Hover, Leaf node=False
    "style2=subtle alpha|size=medium (default)|state=hover|leafNode=false": __body4,
    // figma: Style=Transparent, Size=Medium (Default), State=Hover, Leaf node=False
    "style2=transparent|size=medium (default)|state=hover|leafNode=false": __body5,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Hover, Leaf node=True
    "style2=subtle (default)|size=medium (default)|state=hover|leafNode=true": __body6,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Hover, Leaf node=True
    "style2=subtle alpha|size=medium (default)|state=hover|leafNode=true": __body7,
    // figma: Style=Transparent, Size=Medium (Default), State=Hover, Leaf node=True
    "style2=transparent|size=medium (default)|state=hover|leafNode=true": __body8,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Pressed, Leaf node=False
    "style2=subtle (default)|size=medium (default)|state=pressed|leafNode=false": __body9,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Pressed, Leaf node=False
    "style2=subtle alpha|size=medium (default)|state=pressed|leafNode=false": __body10,
    // figma: Style=Transparent, Size=Medium (Default), State=Pressed, Leaf node=False
    "style2=transparent|size=medium (default)|state=pressed|leafNode=false": __body11,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Pressed, Leaf node=True
    "style2=subtle (default)|size=medium (default)|state=pressed|leafNode=true": __body12,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Pressed, Leaf node=True
    "style2=subtle alpha|size=medium (default)|state=pressed|leafNode=true": __body13,
    // figma: Style=Transparent, Size=Medium (Default), State=Pressed, Leaf node=True
    "style2=transparent|size=medium (default)|state=pressed|leafNode=true": __body14,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Selected, Leaf node=False
    "style2=subtle (default)|size=medium (default)|state=selected|leafNode=false": __body15,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Selected, Leaf node=False
    "style2=subtle alpha|size=medium (default)|state=selected|leafNode=false": __body16,
    // figma: Style=Transparent, Size=Medium (Default), State=Selected, Leaf node=False
    "style2=transparent|size=medium (default)|state=selected|leafNode=false": __body16,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Selected, Leaf node=True
    "style2=subtle (default)|size=medium (default)|state=selected|leafNode=true": __body17,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Selected, Leaf node=True
    "style2=subtle alpha|size=medium (default)|state=selected|leafNode=true": __body18,
    // figma: Style=Transparent, Size=Medium (Default), State=Selected, Leaf node=True
    "style2=transparent|size=medium (default)|state=selected|leafNode=true": __body18,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Disabled, Leaf node=False
    "style2=subtle (default)|size=medium (default)|state=disabled|leafNode=false": __body19,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Disabled, Leaf node=False
    "style2=subtle alpha|size=medium (default)|state=disabled|leafNode=false": __body20,
    // figma: Style=Transparent, Size=Medium (Default), State=Disabled, Leaf node=False
    "style2=transparent|size=medium (default)|state=disabled|leafNode=false": __body21,
    // figma: Style=Subtle (Default), Size=Medium (Default), State=Disabled, Leaf node=True
    "style2=subtle (default)|size=medium (default)|state=disabled|leafNode=true": __body22,
    // figma: Style=Subtle alpha, Size=Medium (Default), State=Disabled, Leaf node=True
    "style2=subtle alpha|size=medium (default)|state=disabled|leafNode=true": __body22,
    // figma: Style=Transparent, Size=Medium (Default), State=Disabled, Leaf node=True
    "style2=transparent|size=medium (default)|state=disabled|leafNode=true": __body23,
    // figma: Style=Subtle (Default), Size=Small, State=Rest, Leaf node=False
    "style2=subtle (default)|size=sm|state=rest|leafNode=false": __body24,
    // figma: Style=Subtle alpha, Size=Small, State=Rest, Leaf node=False
    "style2=subtle alpha|size=sm|state=rest|leafNode=false": __body24,
    // figma: Style=Transparent, Size=Small, State=Rest, Leaf node=False
    "style2=transparent|size=sm|state=rest|leafNode=false": __body24,
    // figma: Style=Subtle (Default), Size=Small, State=Rest, Leaf node=True
    "style2=subtle (default)|size=sm|state=rest|leafNode=true": __body25,
    // figma: Style=Subtle alpha, Size=Small, State=Rest, Leaf node=True
    "style2=subtle alpha|size=sm|state=rest|leafNode=true": __body25,
    // figma: Style=Transparent, Size=Small, State=Rest, Leaf node=True
    "style2=transparent|size=sm|state=rest|leafNode=true": __body25,
    // figma: Style=Subtle (Default), Size=Small, State=Hover, Leaf node=False
    "style2=subtle (default)|size=sm|state=hover|leafNode=false": __body26,
    // figma: Style=Subtle alpha, Size=Small, State=Hover, Leaf node=False
    "style2=subtle alpha|size=sm|state=hover|leafNode=false": __body27,
    // figma: Style=Transparent, Size=Small, State=Hover, Leaf node=False
    "style2=transparent|size=sm|state=hover|leafNode=false": __body28,
    // figma: Style=Subtle (Default), Size=Small, State=Hover, Leaf node=True
    "style2=subtle (default)|size=sm|state=hover|leafNode=true": __body29,
    // figma: Style=Subtle alpha, Size=Small, State=Hover, Leaf node=True
    "style2=subtle alpha|size=sm|state=hover|leafNode=true": __body30,
    // figma: Style=Transparent, Size=Small, State=Hover, Leaf node=True
    "style2=transparent|size=sm|state=hover|leafNode=true": __body31,
    // figma: Style=Subtle (Default), Size=Small, State=Pressed, Leaf node=False
    "style2=subtle (default)|size=sm|state=pressed|leafNode=false": __body32,
    // figma: Style=Subtle alpha, Size=Small, State=Pressed, Leaf node=False
    "style2=subtle alpha|size=sm|state=pressed|leafNode=false": __body33,
    // figma: Style=Transparent, Size=Small, State=Pressed, Leaf node=False
    "style2=transparent|size=sm|state=pressed|leafNode=false": __body34,
    // figma: Style=Subtle (Default), Size=Small, State=Pressed, Leaf node=True
    "style2=subtle (default)|size=sm|state=pressed|leafNode=true": __body35,
    // figma: Style=Subtle alpha, Size=Small, State=Pressed, Leaf node=True
    "style2=subtle alpha|size=sm|state=pressed|leafNode=true": __body36,
    // figma: Style=Transparent, Size=Small, State=Pressed, Leaf node=True
    "style2=transparent|size=sm|state=pressed|leafNode=true": __body37,
    // figma: Style=Subtle (Default), Size=Small, State=Selected, Leaf node=False
    "style2=subtle (default)|size=sm|state=selected|leafNode=false": __body38,
    // figma: Style=Subtle alpha, Size=Small, State=Selected, Leaf node=False
    "style2=subtle alpha|size=sm|state=selected|leafNode=false": __body39,
    // figma: Style=Transparent, Size=Small, State=Selected, Leaf node=False
    "style2=transparent|size=sm|state=selected|leafNode=false": __body39,
    // figma: Style=Subtle (Default), Size=Small, State=Selected, Leaf node=True
    "style2=subtle (default)|size=sm|state=selected|leafNode=true": __body40,
    // figma: Style=Subtle alpha, Size=Small, State=Selected, Leaf node=True
    "style2=subtle alpha|size=sm|state=selected|leafNode=true": __body41,
    // figma: Style=Transparent, Size=Small, State=Selected, Leaf node=True
    "style2=transparent|size=sm|state=selected|leafNode=true": __body41,
    // figma: Style=Subtle (Default), Size=Small, State=Disabled, Leaf node=False
    "style2=subtle (default)|size=sm|state=disabled|leafNode=false": __body42,
    // figma: Style=Subtle alpha, Size=Small, State=Disabled, Leaf node=False
    "style2=subtle alpha|size=sm|state=disabled|leafNode=false": __body42,
    // figma: Style=Transparent, Size=Small, State=Disabled, Leaf node=False
    "style2=transparent|size=sm|state=disabled|leafNode=false": __body42,
    // figma: Style=Subtle (Default), Size=Small, State=Disabled, Leaf node=True
    "style2=subtle (default)|size=sm|state=disabled|leafNode=true": __body43,
    // figma: Style=Subtle alpha, Size=Small, State=Disabled, Leaf node=True
    "style2=subtle alpha|size=sm|state=disabled|leafNode=true": __body43,
    // figma: Style=Transparent, Size=Small, State=Disabled, Leaf node=True
    "style2=transparent|size=sm|state=disabled|leafNode=true": __body43,
  };
  return (__impls[__vkey(props)] ?? __body24)();
}
export default TreeItem;
