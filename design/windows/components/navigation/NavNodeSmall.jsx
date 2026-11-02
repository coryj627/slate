import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron5 } from '../internal/Chevron5.jsx';
import { Folder2 } from '../internal/Folder2.jsx';

// figma node: 9291:3942 NavNode - small (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "group=" + __venc(p.group) + '|' + "selected=" + __venc(p.selected) + '|' + "state=" + __venc(p.state) + '|' + "open=" + __venc(p.open) + '|' + "secondaryActions=" + __venc(p.secondaryActions);

export function NavNodeSmall(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed"]});
  const props = { ..._p, group: _p.group ?? false, selected: _p.selected ?? false, state: _p.state ?? "rest", open: _p.open ?? true, secondaryActions: _p.secondaryActions ?? false, focus: _p.focus ?? false, content: _p.content ?? "Nav content" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "6px 8px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-snudge) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-snudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.iconBeforeFilled ?? <Folder2 size={"20"} theme={"filled"} />}</div>
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
          <span style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 78,
            height: 20,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
          }}>{props.content}</span>
        </div>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Chevron5 expand={false} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 256,
        height: 28,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={256} height={28} viewBox="0 0 256 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 256,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 4 0 L 4 1 L 252 1 L 252 0 L 252 -1 L 4 -1 L 4 0 Z M 256 4 L 255 4 L 255 24 L 256 24 L 257 24 L 257 4 L 256 4 Z M 252 28 L 252 27 L 4 27 L 4 28 L 4 29 L 252 29 L 252 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 256 24 L 255 24 C 255 25.657 253.657 27 252 27 L 252 28 L 252 29 C 254.761 29 257 26.761 257 24 L 256 24 Z M 252 0 L 252 1 C 253.657 1 255 2.343 255 4 L 256 4 L 257 4 C 257 1.239 254.761 -1 252 -1 L 252 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "6px 8px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-snudge) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-snudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.iconBeforeFilled ?? <Folder2 size={"20"} theme={"filled"} />}</div>
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
          <span style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 78,
            height: 20,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-hover)",
          }}>{props.content}</span>
        </div>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Chevron5 expand={false} />}</div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "6px 8px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-snudge) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-snudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.iconBeforeFilled ?? <Folder2 size={"20"} theme={"filled"} />}</div>
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
          <span style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 78,
            height: 20,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-pressed)",
          }}>{props.content}</span>
        </div>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Chevron5 expand={false} />}</div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-160) * 1px)",
        padding: "6px 8px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-snudge) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-snudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
          <span style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 76,
            height: 20,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.content}</span>
        </div>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Chevron5 expand={false} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 256,
        height: 28,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={256} height={28} viewBox="0 0 256 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 256,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 4 0 L 4 1 L 252 1 L 252 0 L 252 -1 L 4 -1 L 4 0 Z M 256 4 L 255 4 L 255 24 L 256 24 L 257 24 L 257 4 L 256 4 Z M 252 28 L 252 27 L 4 27 L 4 28 L 4 29 L 252 29 L 252 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 256 24 L 255 24 C 255 25.657 253.657 27 252 27 L 252 28 L 252 29 C 254.761 29 257 26.761 257 24 L 256 24 Z M 252 0 L 252 1 C 253.657 1 255 2.343 255 4 L 256 4 L 257 4 C 257 1.239 254.761 -1 252 -1 L 252 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "6px 8px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-snudge) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-snudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
          <span style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 76,
            height: 20,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.content}</span>
        </div>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Chevron5 expand={false} />}</div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "6px 8px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-snudge) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-snudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
        <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
          <span style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 76,
            height: 20,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-hover)",
          }}>{props.content}</span>
        </div>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
          }}>{props.icon1 ?? <Chevron5 expand={false} />}</div>
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--vertical-xxs) * 1px)",
      justifyContent: "center",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 32,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          transform: "matrix(0,1,-1,0,8,4)",
          transformOrigin: "0 0",
          width: 24,
          height: 4,
          opacity: 0,
          borderRadius: 2,
        }} />
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-4-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-l) * 1px)",
          padding: "6px 8px 6px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
          <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
            <span style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 76,
              height: 20,
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.content}</span>
          </div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Chevron5 expand={true} />}</div>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 2,
          top: 2,
          width: 256,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
        }}>
          <svg width={256} height={28} viewBox="0 0 256 28" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 256,
            height: 28,
            overflow: "hidden",
            borderRadius: 4,
            color: "var(--neutral-stroke-focus-1-rest-2)",
          }}>
            <path d={"M 4 0 L 4 1 L 252 1 L 252 0 L 252 -1 L 4 -1 L 4 0 Z M 256 4 L 255 4 L 255 24 L 256 24 L 257 24 L 257 4 L 256 4 Z M 252 28 L 252 27 L 4 27 L 4 28 L 4 29 L 252 29 L 252 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 256 24 L 255 24 C 255 25.657 253.657 27 252 27 L 252 28 L 252 29 C 254.761 29 257 26.761 257 24 L 256 24 Z M 252 0 L 252 1 C 253.657 1 255 2.343 255 4 L 256 4 L 257 4 C 257 1.239 254.761 -1 252 -1 L 252 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: 2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--vertical-xxs) * 1px)",
      justifyContent: "center",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 32,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          transform: "matrix(0,1,-1,0,8,4)",
          transformOrigin: "0 0",
          width: 24,
          height: 4,
          opacity: 0,
          borderRadius: 2,
        }} />
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-4-hover)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-l) * 1px)",
          padding: "6px 8px 6px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
          <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
            <span style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 158,
              height: 20,
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.content}</span>
          </div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Chevron5 expand={true} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--vertical-xxs) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--horizontal-none) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-none) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--vertical-xxs) * 1px)",
      justifyContent: "center",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 32,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          transform: "matrix(0,1,-1,0,8,4)",
          transformOrigin: "0 0",
          width: 24,
          height: 4,
          opacity: 0,
          borderRadius: 2,
        }} />
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-4-pressed)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-l) * 1px)",
          padding: "6px 8px 6px 10px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-s) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
          <div style={{ position: "relative", flexGrow: 1, alignSelf: "stretch" }}>
            <span style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 158,
              height: 20,
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 14,
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.content}</span>
          </div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
            }}>{props.icon1 ?? <Chevron5 expand={true} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: 2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xxs) * 1px)",
          padding: "0px 4px 0px 4px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-xs) * 1px)",
          paddingTop: "calc(var(--vertical-none) * 1px)",
          paddingRight: "calc(var(--horizontal-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            transform: "matrix(0,1,-1,0,8,4)",
            transformOrigin: "0 0",
            width: 24,
            height: 4,
            opacity: 0,
            borderRadius: 2,
          }} />
          <div style={{
            position: "relative",
            backgroundColor: "var(--neutral-background-4-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--horizontal-l) * 1px)",
            padding: "0px 6px 0px 46px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--vertical-none) * 1px)",
            paddingRight: "calc(var(--horizontal-snudge) * 1px)",
            paddingBottom: "calc(var(--vertical-none) * 1px)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--horizontal-snudge) * 1px)",
              padding: "4px 0px 6px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--horizontal-none) * 1px)",
              paddingTop: "calc(var(--vertical-xs) * 1px)",
              paddingRight: "calc(var(--horizontal-none) * 1px)",
              paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Nav sub item content</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
            }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.content}</span>
        </div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 224,
        height: 28,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={224} height={28} viewBox="0 0 224 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 224,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 4 0 L 4 1 L 220 1 L 220 0 L 220 -1 L 4 -1 L 4 0 Z M 224 4 L 223 4 L 223 24 L 224 24 L 225 24 L 225 4 L 224 4 Z M 220 28 L 220 27 L 4 27 L 4 28 L 4 29 L 220 29 L 220 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 224 24 L 223 24 C 223 25.657 221.657 27 220 27 L 220 28 L 220 29 C 222.761 29 225 26.761 225 24 L 224 24 Z M 220 0 L 220 1 C 221.657 1 223 2.343 223 4 L 224 4 L 225 4 C 225 1.239 222.761 -1 220 -1 L 220 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
            }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.content}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
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
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
            }}>{props.iconBeforeRegular ?? <Folder2 size={"20"} theme={"regular"} />}</div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.content}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
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
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-snudge) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}>{props.iconBeforeFilled ?? <Folder2 size={"20"} theme={"filled"} />}</div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
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
          }}>{props.content}</span>
        </div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 224,
        height: 28,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={224} height={28} viewBox="0 0 224 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 224,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 4 0 L 4 1 L 220 1 L 220 0 L 220 -1 L 4 -1 L 4 0 Z M 224 4 L 223 4 L 223 24 L 224 24 L 225 24 L 225 4 L 224 4 Z M 220 28 L 220 27 L 4 27 L 4 28 L 4 29 L 220 29 L 220 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 224 24 L 223 24 C 223 25.657 221.657 27 220 27 L 220 28 L 220 29 C 222.761 29 225 26.761 225 24 L 224 24 Z M 220 0 L 220 1 C 221.657 1 223 2.343 223 4 L 224 4 L 225 4 C 225 1.239 222.761 -1 220 -1 L 220 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}>{props.iconBeforeFilled ?? <Folder2 size={"20"} theme={"filled"} />}</div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.content}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
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
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}>{props.iconBeforeFilled ?? <Folder2 size={"20"} theme={"filled"} />}</div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "6px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.content}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
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
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Group=True, Selected=True, State=Rest, Open=False, Secondary actions=False
    "group=true|selected=true|state=rest|open=false|secondaryActions=false": __body0,
    // figma: Group=True, Selected=True, State=Hover, Open=False, Secondary actions=False
    "group=true|selected=true|state=hover|open=false|secondaryActions=false": __body1,
    // figma: Group=True, Selected=True, State=Pressed, Open=False, Secondary actions=False
    "group=true|selected=true|state=pressed|open=false|secondaryActions=false": __body2,
    // figma: Group=True, Selected=False, State=Rest, Open=False, Secondary actions=False
    "group=true|selected=false|state=rest|open=false|secondaryActions=false": __body3,
    // figma: Group=True, Selected=False, State=Hover, Open=False, Secondary actions=False
    "group=true|selected=false|state=hover|open=false|secondaryActions=false": __body4,
    // figma: Group=True, Selected=False, State=Pressed, Open=False, Secondary actions=False
    "group=true|selected=false|state=pressed|open=false|secondaryActions=false": __body5,
    // figma: Group=True, Selected=False, State=Rest, Open=True, Secondary actions=False
    "group=true|selected=false|state=rest|open=true|secondaryActions=false": __body6,
    // figma: Group=True, Selected=False, State=Hover, Open=True, Secondary actions=False
    "group=true|selected=false|state=hover|open=true|secondaryActions=false": __body7,
    // figma: Group=True, Selected=False, State=Pressed, Open=True, Secondary actions=False
    "group=true|selected=false|state=pressed|open=true|secondaryActions=false": __body8,
    // figma: Group=False, Selected=False, State=Rest, Open=False, Secondary actions=False
    "group=false|selected=false|state=rest|open=false|secondaryActions=false": __body9,
    // figma: Group=False, Selected=False, State=Hover, Open=False, Secondary actions=True
    "group=false|selected=false|state=hover|open=false|secondaryActions=true": __body10,
    // figma: Group=False, Selected=False, State=Pressed, Open=False, Secondary actions=True
    "group=false|selected=false|state=pressed|open=false|secondaryActions=true": __body11,
    // figma: Group=False, Selected=True, State=Rest, Open=False, Secondary actions=False
    "group=false|selected=true|state=rest|open=false|secondaryActions=false": __body12,
    // figma: Group=False, Selected=True, State=Hover, Open=False, Secondary actions=True
    "group=false|selected=true|state=hover|open=false|secondaryActions=true": __body13,
    // figma: Group=False, Selected=True, State=Pressed, Open=False, Secondary actions=True
    "group=false|selected=true|state=pressed|open=false|secondaryActions=true": __body14,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default NavNodeSmall;
