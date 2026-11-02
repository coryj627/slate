// figma node: 9064:14012 Section header
export function SectionHeader(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 240,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-4)",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      padding: "8px 0px 8px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "row",
        padding: "0px 8px 0px 8px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-4)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Section header"}</span>
      </div>
    </div>
  );
}
export default SectionHeader;
