import { ArrowSwap2 } from './ArrowSwap2.jsx';

// figma node: 9091:2711 Content placeholder
export function ContentPlaceholder2(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 250,
      height: 44,
      overflow: "hidden",
      backgroundColor: "var(--brand-background-2-rest)",
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
        height: 44,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest)",
          }}>{props.icon1 ?? <ArrowSwap2 size={"16"} theme={"regular"} />}</div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 0px 2px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            textAlign: "center",
            lineHeight: "14px",
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
            whiteSpace: "nowrap",
          }}>{props.text1 ?? "SWAP AREA WITH YOUR COMPONENT"}</span>
        </div>
      </div>
    </div>
  );
}
export default ContentPlaceholder2;
