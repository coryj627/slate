import { __slateNorm } from '../internal/slateProps.jsx';
import { Checkmark3 } from '../internal/Checkmark3.jsx';
import { Image } from '../internal/Image.jsx';

// figma node: 9159:2361 .ListItem (11 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state) + '|' + "type=" + __venc(p.type);

export function ListItem(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed","disabled","focus"],"type":["single select","multi select","header"]});
  const props = { ..._p, checkmark: _p.checkmark ?? true, state: _p.state ?? "rest", type: _p.type ?? "single select", text: _p.text ?? "Action", icon: _p.icon ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "8px 0px 8px 0px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text}</span>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
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
          {props.checkmark && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Checkmark3 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-background-compound-rest)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            {props.checkmark && (
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              overflow: "hidden",
            }}>
              <div style={{
                position: "absolute",
                left: 1,
                top: 1,
                width: 14,
                height: 14,
                backgroundColor: "var(--neutral-foreground-on-brand-rest)",
              }} />
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 16,
                height: 16,
              }}>
                <path d={"M 2 0 C 0.895 0 0 0.895 0 2 L 0 14 C 0 15.105 0.895 16 2 16 L 14 16 C 15.105 16 16 15.105 16 14 L 16 2 C 16 0.895 15.105 0 14 0 L 2 0 Z M 11.8 6.027 L 7.355 10.471 C 7.095 10.732 6.673 10.732 6.412 10.471 L 4.631 8.69 C 4.371 8.43 4.371 8.008 4.631 7.747 C 4.891 7.487 5.314 7.487 5.574 7.747 L 6.884 9.057 L 10.857 5.084 C 11.117 4.824 11.539 4.824 11.8 5.084 C 12.06 5.345 12.06 5.767 11.8 6.027 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
            )}
          </div>
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
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
          {props.checkmark && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.icon1 ?? <Checkmark3 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.icon2 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-background-compound-hover)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              borderRadius: 2,
              boxShadow: "inset 0 0 0 1px var(--neutral-foreground-3-hover)",
            }} />
            {props.checkmark && (
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              overflow: "hidden",
            }}>
              <div style={{
                position: "absolute",
                left: 1,
                top: 1,
                width: 14,
                height: 14,
                backgroundColor: "var(--neutral-foreground-on-brand-rest)",
              }} />
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 16,
                height: 16,
              }}>
                <path d={"M 2 0 C 0.895 0 0 0.895 0 2 L 0 14 C 0 15.105 0.895 16 2 16 L 14 16 C 15.105 16 16 15.105 16 14 L 16 2 C 16 0.895 15.105 0 14 0 L 2 0 Z M 11.8 6.027 L 7.355 10.471 C 7.095 10.732 6.673 10.732 6.412 10.471 L 4.631 8.69 C 4.371 8.43 4.371 8.008 4.631 7.747 C 4.891 7.487 5.314 7.487 5.574 7.747 L 6.884 9.057 L 10.857 5.084 C 11.117 4.824 11.539 4.824 11.8 5.084 C 12.06 5.345 12.06 5.767 11.8 6.027 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
            )}
          </div>
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.icon1 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
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
          {props.checkmark && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.icon1 ?? <Checkmark3 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.icon2 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-background-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              borderRadius: 2,
              boxShadow: "inset 0 0 0 1px var(--neutral-foreground-3-pressed)",
            }} />
            {props.checkmark && (
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              overflow: "hidden",
            }}>
              <div style={{
                position: "absolute",
                left: 1,
                top: 1,
                width: 14,
                height: 14,
                backgroundColor: "var(--neutral-foreground-on-brand-rest)",
              }} />
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 16,
                height: 16,
              }}>
                <path d={"M 2 0 C 0.895 0 0 0.895 0 2 L 0 14 C 0 15.105 0.895 16 2 16 L 14 16 C 15.105 16 16 15.105 16 14 L 16 2 C 16 0.895 15.105 0 14 0 L 2 0 Z M 11.8 6.027 L 7.355 10.471 C 7.095 10.732 6.673 10.732 6.412 10.471 L 4.631 8.69 C 4.371 8.43 4.371 8.008 4.631 7.747 C 4.891 7.487 5.314 7.487 5.574 7.747 L 6.884 9.057 L 10.857 5.084 C 11.117 4.824 11.539 4.824 11.8 5.084 C 12.06 5.345 12.06 5.767 11.8 6.027 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
            )}
          </div>
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.icon1 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
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
          {props.checkmark && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Checkmark3 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon2 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      color: "var(--neutral-foreground-disabled-rest)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              borderRadius: 2,
              boxShadow: "inset 0 0 0 1px var(--neutral-foreground-disabled-rest)",
            }} />
            {props.checkmark && (
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              overflow: "hidden",
            }}>
              <div style={{
                position: "absolute",
                left: 1,
                top: 1,
                width: 14,
                height: 14,
                backgroundColor: "var(--neutral-foreground-on-brand-rest)",
              }} />
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 16,
                height: 16,
              }}>
                <path d={"M 2 0 C 0.895 0 0 0.895 0 2 L 0 14 C 0 15.105 0.895 16 2 16 L 14 16 C 15.105 16 16 15.105 16 14 L 16 2 C 16 0.895 15.105 0 14 0 L 2 0 Z M 11.8 6.027 L 7.355 10.471 C 7.095 10.732 6.673 10.732 6.412 10.471 L 4.631 8.69 C 4.371 8.43 4.371 8.008 4.631 7.747 C 4.891 7.487 5.314 7.487 5.574 7.747 L 6.884 9.057 L 10.857 5.084 C 11.117 4.824 11.539 4.824 11.8 5.084 C 12.06 5.345 12.06 5.767 11.8 6.027 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
            )}
          </div>
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.icon1 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      outline: "3px solid var(--neutral-stroke-focus-1-rest)",
      outlineOffset: "-3px",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
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
          {props.checkmark && (
          <div style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Checkmark3 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 272,
      height: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      outline: "3px solid var(--neutral-stroke-focus-1-rest)",
      outlineOffset: "-3px",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-background-compound-rest)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "inset 0 0 0 2px var(--neutral-stroke-focus-2-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 0px 6px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              borderRadius: 2,
              boxShadow: "inset 0 0 0 1px var(--neutral-foreground-3-rest)",
            }} />
            {props.checkmark && (
            <div style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              overflow: "hidden",
            }}>
              <div style={{
                position: "absolute",
                left: 1,
                top: 1,
                width: 14,
                height: 14,
                backgroundColor: "var(--neutral-foreground-on-brand-rest)",
              }} />
              <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 16,
                height: 16,
              }}>
                <path d={"M 2 0 C 0.895 0 0 0.895 0 2 L 0 14 C 0 15.105 0.895 16 2 16 L 14 16 C 15.105 16 16 15.105 16 14 L 16 2 C 16 0.895 15.105 0 14 0 L 2 0 Z M 11.8 6.027 L 7.355 10.471 C 7.095 10.732 6.673 10.732 6.412 10.471 L 4.631 8.69 C 4.371 8.43 4.371 8.008 4.631 7.747 C 4.891 7.487 5.314 7.487 5.574 7.747 L 6.884 9.057 L 10.857 5.084 C 11.117 4.824 11.539 4.824 11.8 5.084 C 12.06 5.345 12.06 5.767 11.8 6.027 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
            )}
          </div>
        </div>
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 2px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          }}>{props.text}</span>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Rest, Type=Header
    "state=rest|type=header": __body0,
    // figma: State=Rest, Type=Single select
    "state=rest|type=single select": __body1,
    // figma: State=Rest, Type=Multi select
    "state=rest|type=multi select": __body2,
    // figma: State=Hover, Type=Single select
    "state=hover|type=single select": __body3,
    // figma: State=Hover, Type=Multi select
    "state=hover|type=multi select": __body4,
    // figma: State=Pressed, Type=Single select
    "state=pressed|type=single select": __body5,
    // figma: State=Pressed, Type=Multi select
    "state=pressed|type=multi select": __body6,
    // figma: State=Disabled, Type=Single select
    "state=disabled|type=single select": __body7,
    // figma: State=Disabled, Type=Multi select
    "state=disabled|type=multi select": __body8,
    // figma: State=Focus, Type=Single select
    "state=focus|type=single select": __body9,
    // figma: State=Focus, Type=Multi select
    "state=focus|type=multi select": __body10,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default ListItem;
