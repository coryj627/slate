// figma node: 9291:3760 .Divider
export function Divider4(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 264,
      overflow: "hidden",
      borderRadius: 24,
      display: "flex",
      flexDirection: "column",
      padding: "4px 4px 4px 10px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--vertical-none) * 1px)",
      paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--vertical-xs) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-xs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 1,
        backgroundColor: "var(--neutral-stroke-1-rest-4)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
}
export default Divider4;
