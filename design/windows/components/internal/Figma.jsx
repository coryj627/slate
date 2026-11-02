import { __slateNorm } from './slateProps.jsx';
// figma node: 9000:4660 Figma (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Figma(_p = {}) {
  _p = __slateNorm(_p, {"size":["32"],"theme":["color","regular"]});
  const props = { ..._p, size: _p.size ?? "32", theme: _p.theme ?? "color" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <svg width={9.001} height={9.334} viewBox="0 0 9.001 9.334" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 20.666,
        width: 9.001,
        height: 9.334,
        color: "rgb(10,207,131)",
      }}>
        <path d={"M 4.5 9.334 C 6.984 9.334 9.001 7.243 9.001 4.667 L 9.001 0 L 4.5 0 C 2.016 0 0 2.091 0 4.667 C 0 7.243 2.016 9.334 4.5 9.334 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={9.001} height={9.333} viewBox="0 0 9.001 9.333" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 11.333,
        width: 9.001,
        height: 9.333,
        color: "rgb(162,89,255)",
      }}>
        <path d={"M 0 4.667 C 0 2.091 2.016 0 4.5 0 L 9.001 0 L 9.001 9.333 L 4.5 9.333 C 2.016 9.334 0 7.243 0 4.667 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={9.001} height={9.333} viewBox="0 0 9.001 9.333" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 2,
        width: 9.001,
        height: 9.333,
        color: "rgb(242,78,30)",
      }}>
        <path d={"M 0 4.667 C 0 2.091 2.016 0 4.5 0 L 9.001 0 L 9.001 9.333 L 4.5 9.333 C 2.016 9.333 0 7.243 0 4.667 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={9.001} height={9.333} viewBox="0 0 9.001 9.333" fill="none" style={{
        position: "absolute",
        left: 15.999,
        top: 2,
        width: 9.001,
        height: 9.333,
        color: "rgb(255,114,98)",
      }}>
        <path d={"M 0 0 L 4.5 0 C 6.984 0 9.001 2.091 9.001 4.667 C 9.001 7.243 6.984 9.333 4.5 9.333 L 0 9.333 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={9.001} height={9.334} viewBox="0 0 9.001 9.334" fill="none" style={{
        position: "absolute",
        left: 15.999,
        top: 11.333,
        width: 9.001,
        height: 9.334,
        color: "rgb(26,188,254)",
      }}>
        <path d={"M 9.001 4.667 C 9.001 7.243 6.984 9.334 4.5 9.334 C 2.016 9.334 0 7.243 0 4.667 C 0 2.091 2.016 0 4.5 0 C 6.984 0 9.001 2.091 9.001 4.667 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={18.714} height={28} viewBox="0 0 18.714 28" fill="none" style={{
        position: "absolute",
        left: 6.643,
        top: 2,
        width: 18.714,
        height: 28,
      }}>
        <path d={"M 5.179 0 L 13.535 0 C 16.428 0 18.714 2.424 18.714 5.334 C 18.714 7.103 17.869 8.693 16.556 9.666 C 17.869 10.641 18.714 12.231 18.714 14 C 18.714 16.91 16.428 19.334 13.535 19.334 C 12.33 19.334 11.231 18.913 10.358 18.212 L 10.358 22.666 C 10.358 25.576 8.072 28 5.179 28 C 2.286 28 0 25.576 0 22.666 C 0 20.897 0.845 19.307 2.158 18.333 C 0.845 17.359 0 15.769 0 14 C 0 12.231 0.845 10.641 2.158 9.666 C 0.845 8.693 0 7.103 0 5.334 C 0 2.424 2.286 0 5.179 0 Z M 2 5.334 C 2 7.208 3.458 8.666 5.179 8.666 L 8.356 8.666 L 8.356 2 L 5.179 2 C 3.459 2 2 3.459 2 5.334 Z M 8.358 13.885 L 8.358 10.666 L 5.179 10.666 C 3.459 10.666 2 12.126 2 14 C 2 15.874 3.459 17.333 5.179 17.333 L 8.358 17.333 L 8.358 14.115 C 8.357 14.077 8.356 14.039 8.356 14 C 8.356 13.961 8.357 13.923 8.358 13.885 Z M 10.358 14.093 C 10.405 15.922 11.844 17.334 13.535 17.334 C 15.256 17.334 16.714 15.874 16.714 14 C 16.714 12.126 15.256 10.666 13.535 10.666 C 11.844 10.666 10.405 12.078 10.358 13.907 L 10.358 14.093 Z M 13.535 8.666 C 15.256 8.666 16.714 7.208 16.714 5.334 C 16.714 3.459 15.256 2 13.535 2 L 10.358 2 L 10.358 8.666 L 13.535 8.666 Z M 5.179 19.333 C 3.459 19.333 2 20.792 2 22.666 C 2 24.541 3.459 26 5.179 26 C 6.899 26 8.358 24.541 8.358 22.666 L 8.358 19.333 L 5.179 19.333 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=32, Theme=Color
    "size=32|theme=color": __body0,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Figma;
