// figma node: 9064:13999 Focus ring
export function FocusRing(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 154,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 154,
        height: 96,
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }} />
      <div style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 150,
        height: 92,
        borderRadius: 2,
        borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
      }} />
    </div>
  );
}
export default FocusRing;
