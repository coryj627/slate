// figma node: 9328:24862 .Button Slot
export function ButtonSlot(_p = {}) {
  const props = { ..._p, button1: _p.button1 ?? true, button2: _p.button2 ?? true };
  return (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 24,
      display: "flex",
      flexDirection: "row",
      justifyContent: "flex-end",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.button1 && (
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
          }}>
            <svg width={13.001} height={15} viewBox="0 0 13.001 15" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 3,
              width: 13.001,
              height: 15,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 1 10 L 12.5 10 C 12.899 10 13.138 9.555 12.916 9.223 L 10.101 5 L 12.916 0.777 C 13.138 0.445 12.899 0 12.5 0 L 0.5 0 C 0.224 0 0 0.224 0 0.5 L 0 14.5 C 0 14.776 0.224 15 0.5 15 C 0.776 15 1 14.776 1 14.5 L 1 10 Z M 1 9 L 1 1 L 11.566 1 L 9.084 4.723 C 8.972 4.891 8.972 5.109 9.084 5.277 L 11.566 9 L 1 9 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      )}
      {props.button2 && (
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 2px 0px 2px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={10} height={2} viewBox="0 0 10 2" fill="none" style={{
              position: "absolute",
              left: 5,
              top: 9,
              width: 10,
              height: 2,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 2 1 C 2 1.552 1.552 2 1 2 C 0.448 2 0 1.552 0 1 C 0 0.448 0.448 0 1 0 C 1.552 0 2 0.448 2 1 Z M 6 1 C 6 1.552 5.552 2 5 2 C 4.448 2 4 1.552 4 1 C 4 0.448 4.448 0 5 0 C 5.552 0 6 0.448 6 1 Z M 9 2 C 9.552 2 10 1.552 10 1 C 10 0.448 9.552 0 9 0 C 8.448 0 8 0.448 8 1 C 8 1.552 8.448 2 9 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      )}
    </div>
  );
}
export default ButtonSlot;
