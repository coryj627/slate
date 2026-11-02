import { __slateNorm } from './slateProps.jsx';
import { Calendar2 } from './Calendar2.jsx';
import { Person2 } from './Person2.jsx';
import { PresenceBadge } from '../feedback/PresenceBadge.jsx';

// figma node: 9112:8840 Avatar/Avatar (39 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "size=" + __venc(p.size);

export function AvatarAvatar2(_p = {}) {
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 10, top: 10 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 14, top: 14 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 18, top: 18 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 18, top: 18 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 22, top: 22 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 26, top: 26 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs-2) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 28, top: 28 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"sm"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 32, top: 32 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"md"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 40, top: 40 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"md"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 44, top: 44 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"lg"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 52, top: 52 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"lg"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 68, top: 68 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-large huge"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 92, top: 92 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-large huge"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 10, top: 10 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 14, top: 14 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 18, top: 18 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 18, top: 18 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 22, top: 22 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 26, top: 26 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs-2) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 28, top: 28 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"sm"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 32, top: 32 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"md"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 40, top: 40 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"md"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 44, top: 44 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"lg"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 52, top: 52 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"lg"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 68, top: 68 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-large huge"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 92, top: 92 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-large huge"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 10, top: 10 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 14, top: 14 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 18, top: 18 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"tiny"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
      <div style={{ position: "absolute", left: 18, top: 18 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 22, top: 22 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 26, top: 26 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-small"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs-2) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 28, top: 28 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"sm"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 32, top: 32 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"md"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon12 ?? <Calendar2 direction={"ltr"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 40, top: 40 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"md"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 44, top: 44 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"lg"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon16 ?? <Calendar2 direction={"ltr"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 52, top: 52 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"lg"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 68, top: 68 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-large huge"} />}</div>
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
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
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 24,
            height: 24,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.pxRegularBadgeIcon24 ?? <Calendar2 direction={"ltr"} size={"24"} theme={"regular"} />}</div>
      </div>
      )}
      {props.presenceBadge && (
      <div style={{ position: "absolute", left: 92, top: 92 }}>{props.icon1 ?? <PresenceBadge inOffice={true} status={"available"} size={"extra-large huge"} />}</div>
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
export default AvatarAvatar2;
