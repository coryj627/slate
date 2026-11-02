import { ImagesPreviewStep } from './ImagesPreviewStep.jsx';

// figma node: 9380:3542 .Image Preview nav
export function ImagePreviewNav(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 24,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 48,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <ImagesPreviewStep state={"selected"} />}</div>
      <div style={{
          position: "relative",
          width: 40,
          height: 40,
          flexShrink: 0,
        }}>{props.icon2 ?? <ImagesPreviewStep state={"rest"} />}</div>
      <div style={{
          position: "relative",
          width: 40,
          height: 40,
          flexShrink: 0,
        }}>{props.icon3 ?? <ImagesPreviewStep state={"rest"} />}</div>
      <div style={{
          position: "relative",
          width: 40,
          height: 40,
          flexShrink: 0,
        }}>{props.icon4 ?? <ImagesPreviewStep state={"rest"} />}</div>
      <ImagesPreviewStep
        style={{
          position: "relative",
          width: 40,
          height: 40,
          flexShrink: 0,
        }}
        state={"rest"}
      />
    </div>
  );
}
export default ImagePreviewNav;
