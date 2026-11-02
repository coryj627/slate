import { Divider4 } from '../internal/Divider4.jsx';
import { FlexSpace } from '../internal/FlexSpace.jsx';
import { NavNodeSmall } from './NavNodeSmall.jsx';

// figma node: 9291:4392 NavBody - Small
export function NavBodySmall(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 260,
      borderRadius: 24,
      display: "flex",
      flexDirection: "column",
      gap: 2,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <FlexSpace style={{
          position: "relative",
          height: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }} />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={true}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={true}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={true}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "8px 6px 8px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>Section header</span>
      </div>
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={true}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <Divider4 style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }} />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={true}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
      <NavNodeSmall
        style={{
          position: "relative",
          height: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        group={false}
        selected={false}
        state={"rest"}
        open={false}
        secondaryActions={false}
      />
    </div>
  );
}
export default NavBodySmall;
