// figma node: 9380:3532 .Title + BodyCopy
export function TitleBodyCopy(_p = {}) {
  const props = { ..._p, showSubheader: _p.showSubheader ?? true };
  return (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: 360,
      maxHeight: null,
      borderRadius: 24,
      display: "flex",
      flexDirection: "column",
      padding: "0px 0px 12px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 20,
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "wrap",
        alignContent: "space-between",
        boxSizing: "border-box",
        flexShrink: 0,
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
          flexGrow: 1,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Descriptive title"}</span>
      </div>
      {props.showSubheader && (
      <div style={{
        position: "relative",
        height: 20,
        display: "flex",
        flexDirection: "row",
        padding: "0px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "wrap",
        alignContent: "space-between",
        boxSizing: "border-box",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          display: "-webkit-box",
          WebkitBoxOrient: "vertical",
          WebkitLineClamp: 2,
          overflow: "hidden",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>{props.text2 ?? "Description of the content in the carousel."}</span>
      </div>
      )}
    </div>
  );
}
export default TitleBodyCopy;
