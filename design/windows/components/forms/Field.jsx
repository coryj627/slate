import { __slateNorm } from '../internal/slateProps.jsx';
import { Placeholder3 } from '../internal/Placeholder3.jsx';

// figma node: 9122:703 Field (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size);

export function Field(_p = {}) {
  _p = __slateNorm(_p, {"size":["medium (default)","sm","lg"]});
  const props = { ..._p, size: _p.size ?? "medium (default)", helperText: _p.helperText ?? false, statusText: _p.statusText ?? true, label: _p.label ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 250,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.label && (
      <div style={{
        position: "relative",
        height: 26,
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
          }}>{props.labelText ?? "Label"}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--status-danger-foreground-1-rest)",
            flexShrink: 0,
          }}>*</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 16,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
              position: "absolute",
              left: 1,
              top: 1,
              width: 14,
              height: 14,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 7.499 6.5 C 7.499 6.224 7.275 6 6.999 6 C 6.723 6 6.499 6.224 6.499 6.5 L 6.499 9.5 C 6.499 9.776 6.723 10 6.999 10 C 7.275 10 7.499 9.776 7.499 9.5 L 7.499 6.5 Z M 7.748 4.5 C 7.748 4.914 7.413 5.249 6.999 5.249 C 6.585 5.249 6.25 4.914 6.25 4.5 C 6.25 4.086 6.585 3.751 6.999 3.751 C 7.413 3.751 7.748 4.086 7.748 4.5 Z M 7 0 C 3.134 0 0 3.134 0 7 C 0 10.866 3.134 14 7 14 C 10.866 14 14 10.866 14 7 C 14 3.134 10.866 0 7 0 Z M 1 7 C 1 3.686 3.686 1 7 1 C 10.314 1 13 3.686 13 7 C 13 10.314 10.314 13 7 13 C 3.686 13 1 10.314 1 7 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.instance ?? <Placeholder3 text1={"SWAP WITH FORM COMPONENT"} slot={"1"} />}</div>
        {props.statusText && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "2px 0px 2px 0px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
            }}>
              <svg width={9.998} height={10.000} viewBox="0 0 9.998 10.000" fill="none" style={{
                position: "absolute",
                left: 0.996,
                top: 0.996,
                width: 9.998,
                height: 10,
                color: "var(--status-danger-foreground-1-rest)",
              }}>
                <path d={"M 3.585 0.586 C 4.366 -0.195 5.632 -0.195 6.413 0.586 L 9.412 3.586 C 10.193 4.367 10.193 5.633 9.412 6.414 L 6.413 9.414 C 5.632 10.195 4.366 10.195 3.585 9.414 L 0.586 6.414 C -0.146 5.682 -0.192 4.524 0.448 3.738 L 0.586 3.586 L 3.585 0.586 Z M 6.857 3.15 C 6.662 2.955 6.346 2.955 6.15 3.15 L 5.004 4.297 L 3.857 3.15 L 3.779 3.086 C 3.585 2.958 3.321 2.979 3.15 3.15 C 2.979 3.321 2.958 3.585 3.086 3.779 L 3.15 3.857 L 4.297 5.004 L 3.15 6.15 C 2.955 6.346 2.955 6.662 3.15 6.857 C 3.346 7.052 3.662 7.053 3.857 6.857 L 5.004 5.711 L 6.15 6.857 L 6.228 6.922 C 6.423 7.05 6.687 7.028 6.857 6.857 C 7.028 6.687 7.05 6.423 6.922 6.228 L 6.857 6.15 L 5.711 5.004 L 6.857 3.857 C 7.053 3.662 7.052 3.346 6.857 3.15 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--status-danger-foreground-1-rest)",
            flexShrink: 0,
          }}>{props.text1 ?? "Error text"}</span>
        </div>
        )}
        {props.helperText && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
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
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
          }}>{props.text2 ?? "Helper text"}</span>
        </div>
        )}
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 250,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.label && (
      <div style={{
        position: "relative",
        height: 20,
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-end",
          flexWrap: "nowrap",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.labelText ?? "Label"}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--status-danger-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>*</span>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          overflow: "hidden",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "4px 4px 4px 4px",
          justifyContent: "center",
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
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
              position: "absolute",
              left: 5,
              top: 5,
              width: 10,
              height: 10,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 4.5 5.5 C 4.5 5.224 4.724 5 5 5 C 5.276 5 5.5 5.224 5.5 5.5 L 5.5 7 C 5.5 7.276 5.276 7.5 5 7.5 C 4.724 7.5 4.5 7.276 4.5 7 L 4.5 5.5 Z M 5 2.75 C 4.586 2.75 4.25 3.086 4.25 3.5 C 4.25 3.914 4.586 4.25 5 4.25 C 5.414 4.25 5.75 3.914 5.75 3.5 C 5.75 3.086 5.414 2.75 5 2.75 Z M 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 Z M 5 1 C 2.791 1 1 2.791 1 5 C 1 7.209 2.791 9 5 9 C 7.209 9 9 7.209 9 5 C 9 2.791 7.209 1 5 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.instance ?? <Placeholder3 text1={"SWAP WITH FORM COMPONENT"} slot={"1"} />}</div>
        {props.statusText && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "2px 0px 2px 0px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
            }}>
              <svg width={9.998} height={10.000} viewBox="0 0 9.998 10.000" fill="none" style={{
                position: "absolute",
                left: 0.996,
                top: 0.996,
                width: 9.998,
                height: 10,
                color: "var(--status-danger-foreground-1-rest)",
              }}>
                <path d={"M 3.585 0.586 C 4.366 -0.195 5.632 -0.195 6.413 0.586 L 9.412 3.586 C 10.193 4.367 10.193 5.633 9.412 6.414 L 6.413 9.414 C 5.632 10.195 4.366 10.195 3.585 9.414 L 0.586 6.414 C -0.146 5.682 -0.192 4.524 0.448 3.738 L 0.586 3.586 L 3.585 0.586 Z M 6.857 3.15 C 6.662 2.955 6.346 2.955 6.15 3.15 L 5.004 4.297 L 3.857 3.15 L 3.779 3.086 C 3.585 2.958 3.321 2.979 3.15 3.15 C 2.979 3.321 2.958 3.585 3.086 3.779 L 3.15 3.857 L 4.297 5.004 L 3.15 6.15 C 2.955 6.346 2.955 6.662 3.15 6.857 C 3.346 7.052 3.662 7.053 3.857 6.857 L 5.004 5.711 L 6.15 6.857 L 6.228 6.922 C 6.423 7.05 6.687 7.028 6.857 6.857 C 7.028 6.687 7.05 6.423 6.922 6.228 L 6.857 6.15 L 5.711 5.004 L 6.857 3.857 C 7.053 3.662 7.052 3.346 6.857 3.15 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--status-danger-foreground-1-rest)",
            flexShrink: 0,
          }}>{props.text1 ?? "Error text"}</span>
        </div>
        )}
        {props.helperText && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
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
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
          }}>{props.text2 ?? "Helper text"}</span>
        </div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 250,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.label && (
      <div style={{
        position: "relative",
        height: 24,
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 4px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.labelText ?? "Label"}</span>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--status-danger-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>*</span>
        </div>
        <div style={{
          position: "relative",
          width: 24,
          height: 24,
          overflow: "hidden",
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
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 16,
              height: 16,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8.492 6.91 C 8.45 6.677 8.246 6.5 8 6.5 C 7.724 6.5 7.5 6.724 7.5 7 L 7.5 11.502 L 7.508 11.592 C 7.551 11.825 7.755 12.002 8 12.002 C 8.276 12.002 8.5 11.778 8.5 11.502 L 8.5 7 L 8.492 6.91 Z M 8.799 4.75 C 8.799 4.336 8.463 4 8.049 4 C 7.635 4 7.299 4.336 7.299 4.75 C 7.299 5.164 7.635 5.5 8.049 5.5 C 8.463 5.5 8.799 5.164 8.799 4.75 Z M 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 C 12.418 16 16 12.418 16 8 Z M 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 C 4.134 15 1 11.866 1 8 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.instance ?? <Placeholder3 text1={"SWAP WITH FORM COMPONENT"} slot={"1"} />}</div>
        {props.statusText && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "2px 0px 2px 0px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
            }}>
              <svg width={9.998} height={10.000} viewBox="0 0 9.998 10.000" fill="none" style={{
                position: "absolute",
                left: 0.996,
                top: 0.996,
                width: 9.998,
                height: 10,
                color: "var(--status-danger-foreground-1-rest)",
              }}>
                <path d={"M 3.585 0.586 C 4.366 -0.195 5.632 -0.195 6.413 0.586 L 9.412 3.586 C 10.193 4.367 10.193 5.633 9.412 6.414 L 6.413 9.414 C 5.632 10.195 4.366 10.195 3.585 9.414 L 0.586 6.414 C -0.146 5.682 -0.192 4.524 0.448 3.738 L 0.586 3.586 L 3.585 0.586 Z M 6.857 3.15 C 6.662 2.955 6.346 2.955 6.15 3.15 L 5.004 4.297 L 3.857 3.15 L 3.779 3.086 C 3.585 2.958 3.321 2.979 3.15 3.15 C 2.979 3.321 2.958 3.585 3.086 3.779 L 3.15 3.857 L 4.297 5.004 L 3.15 6.15 C 2.955 6.346 2.955 6.662 3.15 6.857 C 3.346 7.052 3.662 7.053 3.857 6.857 L 5.004 5.711 L 6.15 6.857 L 6.228 6.922 C 6.423 7.05 6.687 7.028 6.857 6.857 C 7.028 6.687 7.05 6.423 6.922 6.228 L 6.857 6.15 L 5.711 5.004 L 6.857 3.857 C 7.053 3.662 7.052 3.346 6.857 3.15 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--status-danger-foreground-1-rest)",
            flexShrink: 0,
          }}>{props.text1 ?? "Error text"}</span>
        </div>
        )}
        {props.helperText && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
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
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
          }}>{props.text2 ?? "Helper text"}</span>
        </div>
        )}
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=Medium (Default)
    "size=medium (default)": __body0,
    // figma: Size=Small
    "size=sm": __body1,
    // figma: Size=Large
    "size=lg": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Field;
