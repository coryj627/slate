// figma node: 9291:3757 .Hamburger
export function Hamburger(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 50,
      borderRadius: 24,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 10px 0px 10px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-s) * 1px)",
        padding: "10px 10px 10px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--vertical-mnudge) * 1px)",
        paddingRight: "calc(var(--horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--vertical-mnudge) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 20,
          overflow: "hidden",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <svg width={16} height={11} viewBox="0 0 16 11" fill="none" style={{
            position: "absolute",
            left: 2,
            top: 4,
            width: 16,
            height: 11,
            color: "var(--neutral-foreground-2-rest)",
          }}>
            <path d={"M 0 0.5 C 0 0.224 0.224 0 0.5 0 L 15.5 0 C 15.776 0 16 0.224 16 0.5 C 16 0.776 15.776 1 15.5 1 L 0.5 1 C 0.224 1 0 0.776 0 0.5 Z M 0 5.5 C 0 5.224 0.224 5 0.5 5 L 15.5 5 C 15.776 5 16 5.224 16 5.5 C 16 5.776 15.776 6 15.5 6 L 0.5 6 C 0.224 6 0 5.776 0 5.5 Z M 0.5 10 C 0.224 10 0 10.224 0 10.5 C 0 10.776 0.224 11 0.5 11 L 15.5 11 C 15.776 11 16 10.776 16 10.5 C 16 10.224 15.776 10 15.5 10 L 0.5 10 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
}
export default Hamburger;
