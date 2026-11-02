import { Placeholder3 } from './Placeholder3.jsx';

// figma node: 9291:4423 Flex space
export function FlexSpace(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 252,
      borderRadius: 24,
      backgroundColor: "var(--neutral-background-4-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-mnudge) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexGrow: 1,
          alignSelf: "stretch",
          width: "auto",
          height: "auto",
        }}>{props.flexSpace ?? <Placeholder3 slot={"1"} />}</div>
    </div>
  );
}
export default FlexSpace;
