// figma node: 9064:11893 .Resize handler
export function ResizeHandler(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <svg width={10} height={1} viewBox="0 0 10 1" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0.707,-0.707,0.707,0.707,2,9.071)",
        transformOrigin: "0 0",
        width: 10,
        height: 1,
      }}>
        <path d={"M 10 1 L 0 1 L 0 0 L 10 0 L 10 1 Z"} fill="currentColor" fillRule="evenodd" />
      </svg>
      <svg width={6} height={1} viewBox="0 0 6 1" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0.707,-0.707,0.707,0.707,5.278,9.450)",
        transformOrigin: "0 0",
        width: 6,
        height: 1,
      }}>
        <path d={"M 6 1 L 0 1 L 0 0 L 6 0 L 6 1 Z"} fill="currentColor" fillRule="evenodd" />
      </svg>
    </div>
  );
}
export default ResizeHandler;
