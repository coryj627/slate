import { AnatomyBadge } from './AnatomyBadge.jsx';

// figma node: 9000:6040 Blocks / Text / Variation
export function BlocksTextVariation(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 672,
      backgroundColor: "var(--colors-neutral-grey-white)",
      display: "flex",
      flexDirection: "column",
      gap: 16,
      justifyContent: "center",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <AnatomyBadge
        style={{ position: "relative", flexShrink: 0 }}
        style2={"standard"}
      />
    </div>
  );
}
export default BlocksTextVariation;
