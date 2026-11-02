// figma node: 9328:25164 .Persona Text Ramp/Default
export function PersonaTextRampDefault(_p = {}) {
  const props = { ..._p, tertiaryText: _p.tertiaryText ?? "00:00 AM", tertiaryTextSlot: _p.tertiaryTextSlot ?? true, primaryText: _p.primaryText ?? "Primary text slot", primaryBadgeSlot: _p.primaryBadgeSlot ?? true, secondaryBadgeSlot: _p.secondaryBadgeSlot ?? true, secondaryTextSlot: _p.secondaryTextSlot ?? true, secondaryText: _p.secondaryText ?? "Secondary text slot" };
  return (
    <div className={props.className} style={{
      width: 246,
      borderRadius: 24,
      display: "flex",
      flexDirection: "column",
      padding: "6px 4px 6px 8px",
      justifyContent: "center",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexGrow: 1,
          }}>{props.primaryText}</span>
        </div>
        {props.tertiaryTextSlot && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
          padding: "2px 0px 2px 0px",
          justifyContent: "flex-end",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            width: 54,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            textAlign: "right",
            lineHeight: "16px",
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
          }}>{props.tertiaryText}</span>
        </div>
        )}
      </div>
      {props.secondaryTextSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexGrow: 1,
        }}>{props.secondaryText}</span>
      </div>
      )}
    </div>
  );
}
export default PersonaTextRampDefault;
