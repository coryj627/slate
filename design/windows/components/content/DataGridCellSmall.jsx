import { __slateNorm } from '../internal/slateProps.jsx';
import { AvatarAvatar3 } from '../internal/AvatarAvatar3.jsx';
import { FocusRing } from '../internal/FocusRing.jsx';
import { Folder } from '../internal/Folder.jsx';
import { Placeholder2 } from '../internal/Placeholder2.jsx';

// figma node: 9249:10209 DataGrid cell - Small (7 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "style2=" + __venc(p.style2);

export function DataGridCellSmall(_p = {}) {
  _p = __slateNorm(_p, {"layout":["text","link","single select","multi-select","cell actions","swappable"],"style2":["none","primary","secondary"]});
  const props = { ..._p, layout: _p.layout ?? "text", style2: _p.style2 ?? "none", focus: _p.focus ?? false, avatar24px: _p.avatar24px ?? true, content: _p.content ?? "Content", productIcon: _p.productIcon ?? true, systemIcon: _p.systemIcon ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 200,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      padding: "0px 16px 0px 8px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 33,
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        padding: "5px 0px 7px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 25,
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
              color: "var(--brand-foreground-link-rest)",
              flexShrink: 0,
            }}>Link</span>
            <div style={{
              position: "relative",
              height: 3,
              borderTop: "1px solid var(--brand-foreground-link-rest)",
              borderRight: "1px solid var(--brand-foreground-link-rest)",
              borderBottom: "1px solid var(--brand-foreground-link-rest)",
              borderLeft: "1px solid var(--brand-foreground-link-rest)",
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
              color: "var(--brand-foreground-link-rest)",
            }}>
              <path d={"M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 12.105 1.895 13 3 13 L 11 13 C 12.105 13 13 12.105 13 11 L 13 8.5 C 13 8.224 13.224 8 13.5 8 C 13.776 8 14 8.224 14 8.5 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 C 0 1.343 1.343 0 3 0 L 5.5 0 C 5.776 0 6 0.224 6 0.5 C 6 0.776 5.776 1 5.5 1 L 3 1 Z M 8 0.5 C 8 0.224 8.224 0 8.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 L 14 5.5 C 14 5.776 13.776 6 13.5 6 C 13.224 6 13 5.776 13 5.5 L 13 1.707 L 8.854 5.854 C 8.658 6.049 8.342 6.049 8.146 5.854 C 7.951 5.658 7.951 5.342 8.146 5.146 L 12.293 1 L 8.5 1 C 8.224 1 8 0.776 8 0.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
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
  const __body1 = () => (
    <div className={props.className} style={{
      width: 200,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
      display: "flex",
      flexDirection: "column",
      gap: 10,
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
        height: 33,
        backgroundColor: "var(--brand-background-2-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 17,
          top: 10,
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
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-s-2) * 1px)",
      padding: "0px 16px 0px 8px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.productIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 5px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.productIcon24px ?? <Folder size={"24"} itemCount={false} preview={"none"} />}</div>
      </div>
      )}
      {props.avatar24px && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 0px 5px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <AvatarAvatar3 type={"image"} size={"24"} />}</div>
      </div>
      )}
      {props.systemIcon && (
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "5px 0px 6px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-link-rest)",
          }}>{props.icon20pxReg ?? <Placeholder2 size={"20"} theme={"regular"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "8px 0px 9px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          whiteSpace: "nowrap",
          overflow: "hidden",
          textOverflow: "ellipsis",
          lineHeight: "16px",
          color: "var(--secondary-text)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>{props.content}</span>
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
      width: 200,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "0px 16px 0px 8px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.productIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "4px 0px 5px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.productIcon24px ?? <Folder size={"24"} itemCount={false} preview={"none"} />}</div>
      </div>
      )}
      {props.avatar24px && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 0px 5px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <AvatarAvatar3 presenceBadge={false} activityRing={false} type={"image"} size={"24"} />}</div>
      </div>
      )}
      {props.systemIcon && (
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "5px 0px 6px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-link-rest)",
          }}>{props.icon20pxReg ?? <Placeholder2 size={"20"} theme={"regular"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        overflow: "hidden",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "5px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          overflow: "hidden",
          textOverflow: "ellipsis",
          lineHeight: "20px",
          color: "var(--primary-text)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>{props.content}</span>
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
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 33,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
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
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 32,
          height: 32,
          flexShrink: 0,
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 16,
              height: 16,
              borderRadius: "50%",
              boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest)",
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
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
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
  const __body6 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 33,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--divider)",
      borderRight: "1px solid var(--divider)",
      borderBottom: "1px solid var(--divider)",
      borderLeft: "1px solid var(--divider)",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      padding: "4px 0px 5px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 72,
          height: 33,
        }}>{props.icon1 ?? <FocusRing style={{ transform: "scale(0.468, 0.344)", transformOrigin: "0 0" }} />}</div>
      )}
    </div>
  );
  const __impls = {
    // figma: Layout=Link, Style=Primary
    "layout=link|style2=primary": __body0,
    // figma: Layout=Swappable, Style=Primary
    "layout=swappable|style2=primary": __body1,
    // figma: Layout=Text, Style=Secondary
    "layout=text|style2=secondary": __body2,
    // figma: Layout=Text, Style=Primary
    "layout=text|style2=primary": __body3,
    // figma: Layout=Single select, Style=None
    "layout=single select|style2=none": __body4,
    // figma: Layout=Multi-select, Style=None
    "layout=multi-select|style2=none": __body5,
    // figma: Layout=Cell actions, Style=None
    "layout=cell actions|style2=none": __body6,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default DataGridCellSmall;
