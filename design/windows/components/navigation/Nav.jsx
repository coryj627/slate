import { Hamburger } from '../internal/Hamburger.jsx';
import { Placeholder3 } from '../internal/Placeholder3.jsx';

// figma node: 9291:4389 Nav
export function Nav(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 260,
      overflow: "hidden",
      borderRadius: 24,
      backgroundColor: "var(--neutral-background-4-rest)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--vertical-xxs) * 1px)",
      padding: "0px 0px 4px 0px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-none) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-none) * 1px)",
      paddingBottom: "calc(var(--vertical-xs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{ position: "relative", width: 50, flexShrink: 0 }}>{props.icon1 ?? <Hamburger />}</div>
      <div style={{
          position: "relative",
          height: 954,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.navBody ?? <Placeholder3 slot={"1"} />}</div>
    </div>
  );
}
export default Nav;
