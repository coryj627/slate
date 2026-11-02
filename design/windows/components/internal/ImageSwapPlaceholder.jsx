// figma node: 9026:52 Image swap placeholder
export function ImageSwapPlaceholder(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      overflow: "hidden",
      backgroundColor: "var(--brand-background-2-rest-5)",
      display: "flex",
      flexDirection: "column",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 22,
        display: "flex",
        flexDirection: "column",
        gap: 4,
        padding: "4px 0px 4px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 700,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          textBox: "trim-both cap alphabetic",
          letterSpacing: "0.030em",
          color: "var(--brand-foreground-2-rest-4)",
          flexShrink: 0,
        }}>{props.text1 ?? "40x40"}</span>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 4,
          textAlign: "center",
          lineHeight: 1,
          color: "var(--brand-foreground-2-rest-4)",
          flexShrink: 0,
          whiteSpace: "nowrap",
        }}>{props.text2 ?? "Use component swap to change"}</span>
      </div>
    </div>
  );
}
export default ImageSwapPlaceholder;
