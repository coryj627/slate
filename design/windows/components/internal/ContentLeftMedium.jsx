import { Calendar2 } from './Calendar2.jsx';

// figma node: 9328:25159 .Content Left (Medium)
export function ContentLeftMedium(_p = {}) {
  const props = { ..._p, leftIconSlot: _p.leftIconSlot ?? true, text: _p.text ?? "List content", rightIconSlot: _p.rightIconSlot ?? true, rightIcon1: _p.rightIcon1 ?? true, rightIcon2: _p.rightIcon2 ?? true };
  return (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 24,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIconSlot && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon1 ?? <Calendar2 direction={"ltr"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text}</span>
      </div>
    </div>
  );
}
export default ContentLeftMedium;
