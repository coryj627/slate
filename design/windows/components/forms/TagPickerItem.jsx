import { __slateNorm } from '../internal/slateProps.jsx';
import { Calendar2 } from '../internal/Calendar2.jsx';
import { FocusRing } from '../internal/FocusRing.jsx';

// figma node: 9064:15304 TagPicker/Item (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size);

export function TagPickerItem(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed"],"size":["sm","medium (default)"]});
  const props = { ..._p, state: _p.state ?? "rest", size: _p.size ?? "sm", selection: _p.selection ?? false, media: _p.media ?? true, iconBefore: _p.iconBefore ?? false, primaryString: _p.primaryString ?? "Primary content", secondaryContent: _p.secondaryContent ?? true, secondaryString: _p.secondaryString ?? "Secondary content", endContent: _p.endContent ?? true, focus: _p.focus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          {props.selection && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "16px 2px 16px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-horizontal-l-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-l-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 2,
              backgroundColor: "var(--brand-background-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 5,
                width: 8,
                height: 6,
                color: "var(--neutral-foreground-on-brand-rest)",
              }}>
                <path d={"M 7.765 0.205 C 8.066 0.489 8.08 0.964 7.795 1.265 L 3.545 5.765 C 3.406 5.912 3.213 5.997 3.011 6 C 2.808 6.003 2.613 5.924 2.47 5.78 L 0.22 3.53 C -0.073 3.237 -0.073 2.763 0.22 2.47 C 0.513 2.177 0.987 2.177 1.28 2.47 L 2.985 4.174 L 6.705 0.235 C 6.989 -0.066 7.464 -0.08 7.765 0.205 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
          {props.media && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "8px 2px 8px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-horizontal-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 32,
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
              alignSelf: "stretch",
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
                width: 10,
                height: 10,
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
                  flexShrink: 0,
                  alignSelf: "stretch",
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
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            {props.iconBefore && (
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
                  width: 20,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  height: "auto",
                  color: "var(--neutral-foreground-2-rest)",
                }}>{props.icon3 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
            </div>
            )}
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              padding: "0px 2px 0px 2px",
              justifyContent: "center",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              flexGrow: 1,
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
                flexGrow: 1,
                alignSelf: "stretch",
              }}>{props.primaryString}</span>
              {props.secondaryContent && (
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 12,
                lineHeight: "16px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.secondaryString}</span>
              )}
            </div>
          </div>
          {props.focus && (
          <div style={{
              position: "absolute",
              left: -6,
              top: 0,
              width: 320,
              height: 48,
            }}>
            <FocusRing style={{ transform: "scale(2.078, 0.500)", transformOrigin: "0 0" }} />
          </div>
          )}
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          {props.selection && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "16px 2px 16px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-l-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 2,
              backgroundColor: "var(--brand-background-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 5,
                width: 8,
                height: 6,
                color: "var(--neutral-foreground-on-brand-rest)",
              }}>
                <path d={"M 7.765 0.205 C 8.066 0.489 8.08 0.964 7.795 1.265 L 3.545 5.765 C 3.406 5.912 3.213 5.997 3.011 6 C 2.808 6.003 2.613 5.924 2.47 5.78 L 0.22 3.53 C -0.073 3.237 -0.073 2.763 0.22 2.47 C 0.513 2.177 0.987 2.177 1.28 2.47 L 2.985 4.174 L 6.705 0.235 C 6.989 -0.066 7.464 -0.08 7.765 0.205 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
          {props.media && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "8px 2px 8px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 32,
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
              alignSelf: "stretch",
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
                width: 10,
                height: 10,
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
                  flexShrink: 0,
                  alignSelf: "stretch",
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
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            {props.iconBefore && (
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              padding: "8px 0px 8px 0px",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-horizontal-s-2) * 1px)",
              paddingBottom: "calc(var(--spacing-horizontal-s-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                  position: "relative",
                  width: 20,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  height: "auto",
                  color: "var(--neutral-foreground-2-hover)",
                }}>{props.icon3 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
            </div>
            )}
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              padding: "0px 2px 0px 2px",
              justifyContent: "center",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              flexGrow: 1,
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
                flexGrow: 1,
                alignSelf: "stretch",
              }}>{props.primaryString}</span>
              {props.secondaryContent && (
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 12,
                lineHeight: "16px",
                color: "var(--neutral-foreground-2-hover)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.secondaryString}</span>
              )}
            </div>
          </div>
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "flex-end",
          alignItems: "center",
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
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
                <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
                  position: "absolute",
                  left: 4,
                  top: 4,
                  width: 12,
                  height: 12,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          {props.selection && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "16px 2px 16px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-l-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 2,
              backgroundColor: "var(--brand-background-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 5,
                width: 8,
                height: 6,
                color: "var(--neutral-foreground-on-brand-rest)",
              }}>
                <path d={"M 7.765 0.205 C 8.066 0.489 8.08 0.964 7.795 1.265 L 3.545 5.765 C 3.406 5.912 3.213 5.997 3.011 6 C 2.808 6.003 2.613 5.924 2.47 5.78 L 0.22 3.53 C -0.073 3.237 -0.073 2.763 0.22 2.47 C 0.513 2.177 0.987 2.177 1.28 2.47 L 2.985 4.174 L 6.705 0.235 C 6.989 -0.066 7.464 -0.08 7.765 0.205 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
          {props.media && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "8px 2px 8px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 32,
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
              alignSelf: "stretch",
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
                width: 10,
                height: 10,
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
                  flexShrink: 0,
                  alignSelf: "stretch",
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
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            {props.iconBefore && (
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
                  width: 20,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  height: "auto",
                  color: "var(--neutral-foreground-2-pressed)",
                }}>{props.icon3 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
            </div>
            )}
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              padding: "0px 2px 0px 2px",
              justifyContent: "center",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              flexGrow: 1,
              alignSelf: "stretch",
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 14,
                lineHeight: "20px",
                color: "var(--neutral-foreground-2-pressed)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.primaryString}</span>
              {props.secondaryContent && (
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 12,
                lineHeight: "16px",
                color: "var(--neutral-foreground-2-pressed)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.secondaryString}</span>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          {props.selection && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "8px 2px 8px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 2,
              backgroundColor: "var(--brand-background-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 5,
                width: 8,
                height: 6,
                color: "var(--neutral-foreground-on-brand-rest)",
              }}>
                <path d={"M 7.765 0.205 C 8.066 0.489 8.08 0.964 7.795 1.265 L 3.545 5.765 C 3.406 5.912 3.213 5.997 3.011 6 C 2.808 6.003 2.613 5.924 2.47 5.78 L 0.22 3.53 C -0.073 3.237 -0.073 2.763 0.22 2.47 C 0.513 2.177 0.987 2.177 1.28 2.47 L 2.985 4.174 L 6.705 0.235 C 6.989 -0.066 7.464 -0.08 7.765 0.205 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
          {props.media && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "4px 2px 4px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 24,
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
              alignSelf: "stretch",
            }}>
              <div className="fig-asset-1ca2b212726726ee" style={{
                position: "relative",
                width: 24,
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
                left: 18,
                top: 18,
                width: 6,
                height: 6,
                borderRadius: 9999,
                backgroundColor: "var(--neutral-background-1-rest)",
                boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
              }}>
                <svg width={6} viewBox="0 0 6 6" fill="none" style={{
                  position: "relative",
                  width: 6,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            {props.iconBefore && (
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              padding: "6px 0px 6px 0px",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                  position: "relative",
                  width: 20,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  height: "auto",
                  color: "var(--neutral-foreground-2-rest)",
                }}>{props.icon3 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
            </div>
            )}
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              padding: "6px 2px 6px 2px",
              justifyContent: "center",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexGrow: 1,
              alignSelf: "stretch",
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 14,
                lineHeight: "20px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.primaryString}</span>
            </div>
          </div>
        </div>
      </div>
      {props.focus && (
      <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 320,
          height: 32,
        }}>
        <FocusRing style={{ transform: "scale(2.078, 0.333)", transformOrigin: "0 0" }} />
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          {props.selection && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "8px 2px 8px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 2,
              backgroundColor: "var(--brand-background-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 5,
                width: 8,
                height: 6,
                color: "var(--neutral-foreground-on-brand-rest)",
              }}>
                <path d={"M 7.765 0.205 C 8.066 0.489 8.08 0.964 7.795 1.265 L 3.545 5.765 C 3.406 5.912 3.213 5.997 3.011 6 C 2.808 6.003 2.613 5.924 2.47 5.78 L 0.22 3.53 C -0.073 3.237 -0.073 2.763 0.22 2.47 C 0.513 2.177 0.987 2.177 1.28 2.47 L 2.985 4.174 L 6.705 0.235 C 6.989 -0.066 7.464 -0.08 7.765 0.205 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
          {props.media && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "4px 2px 4px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 24,
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
              alignSelf: "stretch",
            }}>
              <div className="fig-asset-1ca2b212726726ee" style={{
                position: "relative",
                width: 24,
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
                left: 18,
                top: 18,
                width: 6,
                height: 6,
                borderRadius: 9999,
                backgroundColor: "var(--neutral-background-1-rest)",
                boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
              }}>
                <svg width={6} viewBox="0 0 6 6" fill="none" style={{
                  position: "relative",
                  width: 6,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            {props.iconBefore && (
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              padding: "6px 0px 6px 0px",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                  position: "relative",
                  width: 20,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  height: "auto",
                  color: "var(--neutral-foreground-2-hover)",
                }}>{props.icon3 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
            </div>
            )}
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              padding: "6px 2px 6px 2px",
              justifyContent: "center",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexGrow: 1,
              alignSelf: "stretch",
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 14,
                lineHeight: "20px",
                color: "var(--neutral-foreground-2-hover)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.primaryString}</span>
            </div>
          </div>
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          justifyContent: "flex-end",
          alignItems: "center",
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
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
                <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
                  position: "absolute",
                  left: 4,
                  top: 4,
                  width: 12,
                  height: 12,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 320,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 6px 0px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          {props.selection && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "8px 2px 8px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 16,
              borderRadius: 2,
              backgroundColor: "var(--brand-background-1-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 5,
                width: 8,
                height: 6,
                color: "var(--neutral-foreground-on-brand-rest)",
              }}>
                <path d={"M 7.765 0.205 C 8.066 0.489 8.08 0.964 7.795 1.265 L 3.545 5.765 C 3.406 5.912 3.213 5.997 3.011 6 C 2.808 6.003 2.613 5.924 2.47 5.78 L 0.22 3.53 C -0.073 3.237 -0.073 2.763 0.22 2.47 C 0.513 2.177 0.987 2.177 1.28 2.47 L 2.985 4.174 L 6.705 0.235 C 6.989 -0.066 7.464 -0.08 7.765 0.205 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          )}
          {props.media && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            padding: "4px 2px 4px 2px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              width: 24,
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
              alignSelf: "stretch",
            }}>
              <div className="fig-asset-1ca2b212726726ee" style={{
                position: "relative",
                width: 24,
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
                left: 18,
                top: 18,
                width: 6,
                height: 6,
                borderRadius: 9999,
                backgroundColor: "var(--neutral-background-1-rest)",
                boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
              }}>
                <svg width={6} viewBox="0 0 6 6" fill="none" style={{
                  position: "relative",
                  width: 6,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  color: "var(--status-available-foreground-3-rest)",
                }}>
                  <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            {props.iconBefore && (
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              padding: "6px 0px 6px 0px",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                  position: "relative",
                  width: 20,
                  flexShrink: 0,
                  alignSelf: "stretch",
                  height: "auto",
                  color: "var(--neutral-foreground-2-pressed)",
                }}>{props.icon3 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
            </div>
            )}
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "column",
              padding: "6px 2px 6px 2px",
              justifyContent: "center",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexGrow: 1,
              alignSelf: "stretch",
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 14,
                lineHeight: "20px",
                color: "var(--neutral-foreground-2-pressed)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.primaryString}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Rest, Size=Medium (Default)
    "state=rest|size=medium (default)": __body0,
    // figma: State=Hover, Size=Medium (Default)
    "state=hover|size=medium (default)": __body1,
    // figma: State=Pressed, Size=Medium (Default)
    "state=pressed|size=medium (default)": __body2,
    // figma: State=Rest, Size=Small
    "state=rest|size=sm": __body3,
    // figma: State=Hover, Size=Small
    "state=hover|size=sm": __body4,
    // figma: State=Pressed, Size=Small
    "state=pressed|size=sm": __body5,
  };
  return (__impls[__vkey(props)] ?? __body3)();
}
export default TagPickerItem;
