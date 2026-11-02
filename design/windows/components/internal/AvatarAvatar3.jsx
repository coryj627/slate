import { __slateNorm } from './slateProps.jsx';
import { Calendar2 } from './Calendar2.jsx';
import { Person2 } from './Person2.jsx';

// figma node: 9278:11242 Avatar/Avatar (39 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "size=" + __venc(p.size);

export function AvatarAvatar3(_p = {}) {
  _p = __slateNorm(_p, {"type":["image","icon","initials"],"size":["16","20","24","28","32 (default)","36","40","48","56","64","72","96","120"]});
  const props = { ..._p, type: _p.type ?? "image", size: _p.size ?? "16", initialsString1Max: _p.initialsString1Max ?? "M", initialsString2Max: _p.initialsString2Max ?? "MB", activityRing: _p.activityRing ?? false, badgeIcon: _p.badgeIcon ?? false, presenceBadge: _p.presenceBadge ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 20,
        height: 20,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 16,
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 10,
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
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 24,
        height: 24,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 20,
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 14,
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
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 28,
        height: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
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
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 18,
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
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 32,
        height: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 28,
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 18,
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
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 36,
        height: 36,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
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
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 20,
        top: 20,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 1px 1px 1px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 22,
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
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 40,
        height: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 36,
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 24,
        top: 24,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 1px 1px 1px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 26,
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
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 44,
        height: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 40,
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 28,
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
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 52,
        height: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 48,
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 30,
        top: 30,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 32,
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
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      borderRadius: 9999,
      borderTop: "3px solid rgba(255,255,255,0)",
      borderRight: "3px solid rgba(255,255,255,0)",
      borderBottom: "3px solid rgba(255,255,255,0)",
      borderLeft: "3px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 62,
        height: 62,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 56,
        borderRadius: 9999,
        borderTop: "3px solid rgba(255,255,255,0)",
        borderRight: "3px solid rgba(255,255,255,0)",
        borderBottom: "3px solid rgba(255,255,255,0)",
        borderLeft: "3px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 38,
        top: 38,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 40,
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
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 70,
        height: 70,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 64,
        borderRadius: 9999,
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 42,
        top: 42,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 44,
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
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 78,
        height: 78,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 72,
        borderRadius: 9999,
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 50,
        top: 50,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 52,
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
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 96,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 102,
        height: 102,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 96,
        borderRadius: 9999,
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 66,
        top: 66,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 68,
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
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 126,
        height: 126,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div className="fig-asset-1ca2b212726726ee" style={{
        position: "relative",
        width: 120,
        borderRadius: 9999,
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 90,
        top: 90,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 92,
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
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 20,
        height: 20,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 16,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 2,
          width: 12,
          height: 12,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular12 ?? <Person2 size={"12"} theme={"regular"} />}</div>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 10,
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
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 24,
        height: 24,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 20,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 2,
          width: 16,
          height: 16,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular16 ?? <Person2 size={"16"} theme={"regular"} />}</div>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 14,
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
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 28,
        height: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 24,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 4,
          width: 16,
          height: 16,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular16 ?? <Person2 size={"16"} theme={"regular"} />}</div>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 18,
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
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 32,
        height: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 28,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 4,
          width: 20,
          height: 20,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular20 ?? <Person2 size={"20"} theme={"regular"} />}</div>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 18,
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
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 36,
        height: 36,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 32,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 6,
          width: 20,
          height: 20,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular20 ?? <Person2 size={"20"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 20,
        top: 20,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 1px 1px 1px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 22,
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
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 40,
        height: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 36,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 8,
          width: 20,
          height: 20,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular20 ?? <Person2 size={"20"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 24,
        top: 24,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 1px 1px 1px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 26,
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
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 44,
        height: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 40,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <div style={{
          position: "absolute",
          left: 10,
          top: 10,
          width: 20,
          height: 20,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular20 ?? <Person2 size={"20"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 28,
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
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 52,
        height: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 48,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <div style={{
          position: "absolute",
          left: 12,
          top: 12,
          width: 24,
          height: 24,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular24 ?? <Person2 size={"24"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 30,
        top: 30,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 32,
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
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      borderRadius: 9999,
      borderTop: "3px solid rgba(255,255,255,0)",
      borderRight: "3px solid rgba(255,255,255,0)",
      borderBottom: "3px solid rgba(255,255,255,0)",
      borderLeft: "3px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 62,
        height: 62,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 56,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "3px solid rgba(255,255,255,0)",
        borderRight: "3px solid rgba(255,255,255,0)",
        borderBottom: "3px solid rgba(255,255,255,0)",
        borderLeft: "3px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <div style={{
          position: "absolute",
          left: 14,
          top: 14,
          width: 28,
          height: 28,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular28 ?? <Person2 size={"28"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 38,
        top: 38,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 40,
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
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 70,
        height: 70,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 64,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 16,
          width: 32,
          height: 32,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular32 ?? <Person2 size={"32"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 42,
        top: 42,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 44,
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
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 78,
        height: 78,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 72,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
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
          top: 20,
          width: 32,
          height: 32,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular32 ?? <Person2 size={"32"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 50,
        top: 50,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 52,
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
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 102,
        height: 102,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 96,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <div style={{
          position: "absolute",
          left: 24,
          top: 24,
          width: 48,
          height: 48,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular48 ?? <Person2 size={"48"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 66,
        top: 66,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 68,
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
      )}
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 126,
        height: 126,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 120,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <div style={{
          position: "absolute",
          left: 36,
          top: 36,
          width: 48,
          height: 48,
          color: "rgb(97,97,97)",
        }}>{props.pxRegular48 ?? <Person2 size={"48"} theme={"regular"} />}</div>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 90,
        top: 90,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 92,
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
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 20,
        height: 20,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 16,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 1,
        width: 16,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "14px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString1Max}</span>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 10,
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
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 24,
        height: 24,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 20,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 3,
        width: 20,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "14px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 14,
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
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 28,
        height: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 24,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 5,
        width: 24,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "14px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 18,
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
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 32,
        height: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 28,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 6,
        width: 28,
        height: 16,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 12,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "16px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 18,
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
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
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
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 36,
        height: 36,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 32,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 6,
        width: 32,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "20px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 20,
        top: 20,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 1px 1px 1px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 22,
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
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 40,
        height: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 36,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 8,
        width: 36,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "20px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 24,
        top: 24,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "1px 1px 1px 1px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 26,
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
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 44,
        height: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 40,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 10,
        width: 40,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "20px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 28,
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
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      borderRadius: 9999,
      borderTop: "2px solid rgba(255,255,255,0)",
      borderRight: "2px solid rgba(255,255,255,0)",
      borderBottom: "2px solid rgba(255,255,255,0)",
      borderLeft: "2px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -2,
        top: -2,
        width: 52,
        height: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "2px solid rgb(15,108,189)",
        borderRight: "2px solid rgb(15,108,189)",
        borderBottom: "2px solid rgb(15,108,189)",
        borderLeft: "2px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 48,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 14,
        width: 48,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "20px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 30,
        top: 30,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 32,
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
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      borderRadius: 9999,
      borderTop: "3px solid rgba(255,255,255,0)",
      borderRight: "3px solid rgba(255,255,255,0)",
      borderBottom: "3px solid rgba(255,255,255,0)",
      borderLeft: "3px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 62,
        height: 62,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 56,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "3px solid rgba(255,255,255,0)",
        borderRight: "3px solid rgba(255,255,255,0)",
        borderBottom: "3px solid rgba(255,255,255,0)",
        borderLeft: "3px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 17,
        width: 56,
        height: 22,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "22px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 38,
        top: 38,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 40,
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
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 70,
        height: 70,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 64,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 18,
        width: 64,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "28px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 42,
        top: 42,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 44,
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
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 78,
        height: 78,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 72,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 72,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "28px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 50,
        top: 50,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 52,
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
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 102,
        height: 102,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 96,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 32,
        width: 96,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "32px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 66,
        top: 66,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 68,
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
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      borderRadius: 9999,
      borderTop: "4px solid rgba(255,255,255,0)",
      borderRight: "4px solid rgba(255,255,255,0)",
      borderBottom: "4px solid rgba(255,255,255,0)",
      borderLeft: "4px solid rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.activityRing && (
      <div style={{
        position: "absolute",
        left: -3,
        top: -3,
        width: 126,
        height: 126,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "3px solid rgb(15,108,189)",
        borderRight: "3px solid rgb(15,108,189)",
        borderBottom: "3px solid rgb(15,108,189)",
        borderLeft: "3px solid rgb(15,108,189)",
      }} />
      )}
      <div style={{
        position: "relative",
        width: 120,
        borderRadius: 9999,
        backgroundColor: "rgb(230,230,230)",
        borderTop: "4px solid rgba(255,255,255,0)",
        borderRight: "4px solid rgba(255,255,255,0)",
        borderBottom: "4px solid rgba(255,255,255,0)",
        borderLeft: "4px solid rgba(255,255,255,0)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      <span style={{
        position: "absolute",
        left: 0,
        top: 44,
        width: 120,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "32px",
        color: "rgb(97,97,97)",
      }}>{props.initialsString2Max}</span>
      {props.badgeIcon && (
      <div style={{
        position: "absolute",
        left: 90,
        top: 90,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
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
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{
        position: "absolute",
        left: 92,
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
      )}
    </div>
  );
  const __impls = {
    // figma: Type=Image, Size=16
    "type=image|size=16": __body0,
    // figma: Type=Image, Size=20
    "type=image|size=20": __body1,
    // figma: Type=Image, Size=24
    "type=image|size=24": __body2,
    // figma: Type=Image, Size=28
    "type=image|size=28": __body3,
    // figma: Type=Image, Size=32 (Default)
    "type=image|size=32 (default)": __body4,
    // figma: Type=Image, Size=36
    "type=image|size=36": __body5,
    // figma: Type=Image, Size=40
    "type=image|size=40": __body6,
    // figma: Type=Image, Size=48
    "type=image|size=48": __body7,
    // figma: Type=Image, Size=56
    "type=image|size=56": __body8,
    // figma: Type=Image, Size=64
    "type=image|size=64": __body9,
    // figma: Type=Image, Size=72
    "type=image|size=72": __body10,
    // figma: Type=Image, Size=96
    "type=image|size=96": __body11,
    // figma: Type=Image, Size=120
    "type=image|size=120": __body12,
    // figma: Type=Icon, Size=16
    "type=icon|size=16": __body13,
    // figma: Type=Icon, Size=20
    "type=icon|size=20": __body14,
    // figma: Type=Icon, Size=24
    "type=icon|size=24": __body15,
    // figma: Type=Icon, Size=28
    "type=icon|size=28": __body16,
    // figma: Type=Icon, Size=32 (Default)
    "type=icon|size=32 (default)": __body17,
    // figma: Type=Icon, Size=36
    "type=icon|size=36": __body18,
    // figma: Type=Icon, Size=40
    "type=icon|size=40": __body19,
    // figma: Type=Icon, Size=48
    "type=icon|size=48": __body20,
    // figma: Type=Icon, Size=56
    "type=icon|size=56": __body21,
    // figma: Type=Icon, Size=64
    "type=icon|size=64": __body22,
    // figma: Type=Icon, Size=72
    "type=icon|size=72": __body23,
    // figma: Type=Icon, Size=96
    "type=icon|size=96": __body24,
    // figma: Type=Icon, Size=120
    "type=icon|size=120": __body25,
    // figma: Type=Initials, Size=16
    "type=initials|size=16": __body26,
    // figma: Type=Initials, Size=20
    "type=initials|size=20": __body27,
    // figma: Type=Initials, Size=24
    "type=initials|size=24": __body28,
    // figma: Type=Initials, Size=28
    "type=initials|size=28": __body29,
    // figma: Type=Initials, Size=32 (Default)
    "type=initials|size=32 (default)": __body30,
    // figma: Type=Initials, Size=36
    "type=initials|size=36": __body31,
    // figma: Type=Initials, Size=40
    "type=initials|size=40": __body32,
    // figma: Type=Initials, Size=48
    "type=initials|size=48": __body33,
    // figma: Type=Initials, Size=56
    "type=initials|size=56": __body34,
    // figma: Type=Initials, Size=64
    "type=initials|size=64": __body35,
    // figma: Type=Initials, Size=72
    "type=initials|size=72": __body36,
    // figma: Type=Initials, Size=96
    "type=initials|size=96": __body37,
    // figma: Type=Initials, Size=120
    "type=initials|size=120": __body38,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default AvatarAvatar3;
