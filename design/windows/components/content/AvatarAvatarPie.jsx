import { __slateNorm } from '../internal/slateProps.jsx';
import { AvatarAvatar3 } from '../internal/AvatarAvatar3.jsx';

// figma node: 9278:11492 Avatar/Avatar pie (26 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "size=" + __venc(p.size);

export function AvatarAvatarPie(_p = {}) {
  _p = __slateNorm(_p, {"layout":["2 avatars","3 avatars"],"size":["16","20","24","28","32 (default)","36","40","48","56","64","72","96","120"]});
  const props = { ..._p, layout: _p.layout ?? "2 avatars", size: _p.size ?? "16" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 32,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 6,
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
      <div style={{
          position: "relative",
          width: 16,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"32 (default)"} />}</div>
      <div style={{
        position: "absolute",
        left: 15,
        top: 0,
        width: 2,
        height: 32,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      minWidth: 16,
      minHeight: 16,
      maxWidth: 16,
      maxHeight: 16,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 16,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 2,
          top: 10,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 8,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString1Max={"E"} type={"initials"} size={"16"} />}</div>
      <div style={{
        position: "absolute",
        left: 7,
        top: 0,
        width: 2,
        height: 16,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      minWidth: 20,
      minHeight: 20,
      maxWidth: 20,
      maxHeight: 20,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 20,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 4,
          top: 14,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 10,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"20"} />}</div>
      <div style={{
        position: "absolute",
        left: 9,
        top: 0,
        width: 2,
        height: 20,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 24,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 6,
          top: 18,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"24"} />}</div>
      <div style={{
        position: "absolute",
        left: 11,
        top: 0,
        width: 2,
        height: 24,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 14,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 28,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 4,
          top: 18,
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
      <div style={{
          position: "relative",
          width: 14,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"28"} />}</div>
      <div style={{
        position: "absolute",
        left: 13,
        top: 0,
        width: 2,
        height: 28,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 18,
        overflow: "hidden",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 36,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 8,
          top: 26,
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
      <div style={{
          position: "relative",
          width: 18,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"36"} />}</div>
      <div style={{
        position: "absolute",
        left: 17,
        top: 0,
        width: 2,
        height: 36,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        overflow: "hidden",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 40,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 8,
          top: 28,
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
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"40"} />}</div>
      <div style={{
        position: "absolute",
        left: 19,
        top: 0,
        width: 2,
        height: 40,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        overflow: "hidden",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 48,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 8,
          top: 32,
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
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 24,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"48"} />}</div>
      <div style={{
        position: "absolute",
        left: 23,
        top: 0,
        width: 2,
        height: 48,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "3px solid rgba(255,255,255,0)",
      borderRight: "3px solid rgba(255,255,255,0)",
      borderBottom: "3px solid rgba(255,255,255,0)",
      borderLeft: "3px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        overflow: "hidden",
        borderTop: "3px solid rgba(255,255,255,0)",
        borderRight: "3px solid rgba(255,255,255,0)",
        borderBottom: "3px solid rgba(255,255,255,0)",
        borderLeft: "3px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 56,
          borderTop: "3px solid rgba(255,255,255,0)",
          borderRight: "3px solid rgba(255,255,255,0)",
          borderBottom: "3px solid rgba(255,255,255,0)",
          borderLeft: "3px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 12,
          top: 40,
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
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 28,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"56"} />}</div>
      <div style={{
        position: "absolute",
        left: 27,
        top: 0,
        width: 2,
        height: 56,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 64,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 12,
          top: 44,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"64"} />}</div>
      <div style={{
        position: "absolute",
        left: 31,
        top: 0,
        width: 2,
        height: 64,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 36,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 72,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 16,
          top: 52,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "relative",
          width: 36,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"72"} />}</div>
      <div style={{
        position: "absolute",
        left: 35,
        top: 0,
        width: 2,
        height: 72,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 48,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 96,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 20,
          top: 68,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <AvatarAvatar3
        style={{
          position: "relative",
          width: 48,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        initialsString2Max={"EW"}
        initialsString1Max={"E"}
        type={"initials"}
        size={"96"}
      />
      <div style={{
        position: "absolute",
        left: 47,
        top: 0,
        width: 2,
        height: 96,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 60,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 120,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 32,
          top: 92,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <AvatarAvatar3
        style={{
          position: "relative",
          width: 60,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        initialsString2Max={"EW"}
        initialsString1Max={"E"}
        type={"initials"}
        size={"120"}
      />
      <div style={{
        position: "absolute",
        left: 59,
        top: 0,
        width: 2,
        height: 120,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 16,
      minWidth: 16,
      minHeight: 16,
      maxWidth: 16,
      maxHeight: 16,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 16,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 2,
          top: 10,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 8,
          top: 0,
          width: 8,
          height: 8,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"20"} />}</div>
      <div style={{
          position: "absolute",
          left: 8,
          top: 8,
          width: 8,
          height: 8,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString1Max={"E"} type={"icon"} size={"16"} />}</div>
      <div style={{
        position: "absolute",
        left: 7,
        top: 0,
        width: 2,
        height: 16,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 7,
        top: 7,
        width: 9,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      minWidth: 20,
      minHeight: 20,
      maxWidth: 20,
      maxHeight: 20,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 20,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 4,
          top: 14,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 10,
          top: 0,
          width: 10,
          height: 10,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"20"} />}</div>
      <div style={{
          position: "absolute",
          left: 10,
          top: 10,
          width: 10,
          height: 10,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"icon"} size={"20"} />}</div>
      <div style={{
        position: "absolute",
        left: 9,
        top: 0,
        width: 2,
        height: 20,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 9,
        top: 9,
        width: 11,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 24,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 6,
          top: 18,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 12,
          top: 0,
          width: 12,
          height: 12,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"24"} />}</div>
      <div style={{
          position: "absolute",
          left: 12,
          top: 12,
          width: 12,
          height: 12,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"icon"} size={"24"} />}</div>
      <div style={{
        position: "absolute",
        left: 11,
        top: 0,
        width: 2,
        height: 24,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 11,
        top: 11,
        width: 13,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 14,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 28,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 4,
          top: 18,
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
      <div style={{
          position: "absolute",
          left: 14,
          top: 0,
          width: 14,
          height: 14,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"28"} />}</div>
      <div style={{
          position: "absolute",
          left: 14,
          top: 14,
          width: 14,
          height: 14,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"icon"} size={"28"} />}</div>
      <div style={{
        position: "absolute",
        left: 13,
        top: 0,
        width: 2,
        height: 28,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 13,
        top: 13,
        width: 15,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        overflow: "hidden",
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
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 32,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 6,
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
      <div style={{
          position: "absolute",
          left: 16,
          top: 0,
          width: 16,
          height: 16,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"16"} />}</div>
      <div style={{
          position: "absolute",
          left: 16,
          top: 16,
          width: 16,
          height: 16,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"icon"} size={"16"} />}</div>
      <div style={{
        position: "absolute",
        left: 15,
        top: 15,
        width: 17,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 15,
        top: 0,
        width: 2,
        height: 32,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 18,
        overflow: "hidden",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 36,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 8,
          top: 26,
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
      <div style={{
          position: "absolute",
          left: 18,
          top: 0,
          width: 18,
          height: 18,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"20"} />}</div>
      <div style={{
          position: "absolute",
          left: 18,
          top: 18,
          width: 18,
          height: 18,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"icon"} size={"20"} />}</div>
      <div style={{
        position: "absolute",
        left: 17,
        top: 17,
        width: 19,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 17,
        top: 0,
        width: 2,
        height: 36,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        overflow: "hidden",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 40,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 8,
          top: 28,
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
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 20,
          top: 0,
          width: 20,
          height: 20,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"initials"} size={"20"} />}</div>
      <div style={{
          position: "absolute",
          left: 20,
          top: 20,
          width: 20,
          height: 20,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"E"} initialsString1Max={"E"} type={"icon"} size={"20"} />}</div>
      <div style={{
        position: "absolute",
        left: 19,
        top: 19,
        width: 21,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 19,
        top: 0,
        width: 2,
        height: 40,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        overflow: "hidden",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 48,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 8,
          top: 32,
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
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 24,
          top: 0,
          width: 24,
          height: 24,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"24"} />}</div>
      <div style={{
          position: "absolute",
          left: 24,
          top: 24,
          width: 24,
          height: 24,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"icon"} size={"24"} />}</div>
      <div style={{
        position: "absolute",
        left: 23,
        top: 23,
        width: 25,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 23,
        top: 0,
        width: 2,
        height: 48,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "3px solid rgba(255,255,255,0)",
      borderRight: "3px solid rgba(255,255,255,0)",
      borderBottom: "3px solid rgba(255,255,255,0)",
      borderLeft: "3px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        overflow: "hidden",
        borderTop: "3px solid rgba(255,255,255,0)",
        borderRight: "3px solid rgba(255,255,255,0)",
        borderBottom: "3px solid rgba(255,255,255,0)",
        borderLeft: "3px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 56,
          borderTop: "3px solid rgba(255,255,255,0)",
          borderRight: "3px solid rgba(255,255,255,0)",
          borderBottom: "3px solid rgba(255,255,255,0)",
          borderLeft: "3px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 12,
          top: 40,
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
            width: 16,
            height: 16,
            flexShrink: 0,
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 16,
              height: 16,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 28,
          top: 0,
          width: 28,
          height: 28,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"28"} />}</div>
      <div style={{
          position: "absolute",
          left: 28,
          top: 28,
          width: 28,
          height: 28,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"icon"} size={"28"} />}</div>
      <div style={{
        position: "absolute",
        left: 27,
        top: 0,
        width: 2,
        height: 56,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 27,
        top: 27,
        width: 29,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 64,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 12,
          top: 44,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 32,
          top: 0,
          width: 32,
          height: 32,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"32 (default)"} />}</div>
      <div style={{
          position: "absolute",
          left: 32,
          top: 32,
          width: 32,
          height: 32,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"icon"} size={"32 (default)"} />}</div>
      <div style={{
        position: "absolute",
        left: 31,
        top: 31,
        width: 33,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 31,
        top: 0,
        width: 2,
        height: 64,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 36,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 72,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 16,
          top: 52,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 36,
          top: 0,
          width: 36,
          height: 36,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"36"} />}</div>
      <div style={{
          position: "absolute",
          left: 36,
          top: 36,
          width: 36,
          height: 36,
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"icon"} size={"36"} />}</div>
      <div style={{
        position: "absolute",
        left: 35,
        top: 35,
        width: 37,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 35,
        top: 0,
        width: 2,
        height: 72,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 48,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 96,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 20,
          top: 68,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 48,
          top: 0,
          width: 48,
          height: 48,
        }}>{props.icon1 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"48"} />}</div>
      <div style={{
          position: "absolute",
          left: 48,
          top: 48,
          width: 48,
          height: 48,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"icon"} size={"48"} />}</div>
      <div style={{
        position: "absolute",
        left: 47,
        top: 47,
        width: 49,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 47,
        top: 0,
        width: 2,
        height: 96,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      overflow: "hidden",
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 60,
        overflow: "hidden",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div className="fig-asset-97fee250e209bbaa" style={{
          position: "relative",
          width: 120,
          borderTop: "4px solid rgba(255,255,255,0)",
          borderRight: "4px solid rgba(255,255,255,0)",
          borderBottom: "4px solid rgba(255,255,255,0)",
          borderLeft: "4px solid rgba(255,255,255,0)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        <div style={{
          position: "absolute",
          left: 32,
          top: 92,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
            position: "relative",
            width: 28,
            height: 28,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
          position: "absolute",
          left: 60,
          top: 0,
          width: 60,
          height: 60,
        }}>{props.icon1 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"initials"} size={"64"} />}</div>
      <div style={{
          position: "absolute",
          left: 60,
          top: 60,
          width: 60,
          height: 60,
        }}>{props.icon2 ?? <AvatarAvatar3 initialsString2Max={"EW"} initialsString1Max={"E"} type={"icon"} size={"64"} />}</div>
      <div style={{
        position: "absolute",
        left: 59,
        top: 59,
        width: 61,
        height: 2,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
      <div style={{
        position: "absolute",
        left: 59,
        top: 0,
        width: 2,
        height: 120,
        backgroundColor: "var(--neutral-stroke-on-brand-1-rest)",
      }} />
    </div>
  );
  const __impls = {
    // figma: Layout=2 avatars, Size=32 (Default)
    "layout=2 avatars|size=32 (default)": __body0,
    // figma: Layout=2 avatars, Size=16
    "layout=2 avatars|size=16": __body1,
    // figma: Layout=2 avatars, Size=20
    "layout=2 avatars|size=20": __body2,
    // figma: Layout=2 avatars, Size=24
    "layout=2 avatars|size=24": __body3,
    // figma: Layout=2 avatars, Size=28
    "layout=2 avatars|size=28": __body4,
    // figma: Layout=2 avatars, Size=36
    "layout=2 avatars|size=36": __body5,
    // figma: Layout=2 avatars, Size=40
    "layout=2 avatars|size=40": __body6,
    // figma: Layout=2 avatars, Size=48
    "layout=2 avatars|size=48": __body7,
    // figma: Layout=2 avatars, Size=56
    "layout=2 avatars|size=56": __body8,
    // figma: Layout=2 avatars, Size=64
    "layout=2 avatars|size=64": __body9,
    // figma: Layout=2 avatars, Size=72
    "layout=2 avatars|size=72": __body10,
    // figma: Layout=2 avatars, Size=96
    "layout=2 avatars|size=96": __body11,
    // figma: Layout=2 avatars, Size=120
    "layout=2 avatars|size=120": __body12,
    // figma: Layout=3 avatars, Size=16
    "layout=3 avatars|size=16": __body13,
    // figma: Layout=3 avatars, Size=20
    "layout=3 avatars|size=20": __body14,
    // figma: Layout=3 avatars, Size=24
    "layout=3 avatars|size=24": __body15,
    // figma: Layout=3 avatars, Size=28
    "layout=3 avatars|size=28": __body16,
    // figma: Layout=3 avatars, Size=32 (Default)
    "layout=3 avatars|size=32 (default)": __body17,
    // figma: Layout=3 avatars, Size=36
    "layout=3 avatars|size=36": __body18,
    // figma: Layout=3 avatars, Size=40
    "layout=3 avatars|size=40": __body19,
    // figma: Layout=3 avatars, Size=48
    "layout=3 avatars|size=48": __body20,
    // figma: Layout=3 avatars, Size=56
    "layout=3 avatars|size=56": __body21,
    // figma: Layout=3 avatars, Size=64
    "layout=3 avatars|size=64": __body22,
    // figma: Layout=3 avatars, Size=72
    "layout=3 avatars|size=72": __body23,
    // figma: Layout=3 avatars, Size=96
    "layout=3 avatars|size=96": __body24,
    // figma: Layout=3 avatars, Size=120
    "layout=3 avatars|size=120": __body25,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default AvatarAvatarPie;
