import { __slateNorm } from './slateProps.jsx';
// figma node: 9121:7552 Branch Fork (8 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function BranchFork(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24","32"],"theme":["regular","filled"]});
  const props = { ..._p, size: _p.size ?? "16", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={14} viewBox="0 0 12 14" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 1,
        width: 12,
        height: 14,
      }}>
        <path d={"M 3 4.95 C 4.141 4.718 5 3.709 5 2.5 C 5 1.119 3.881 0 2.5 0 C 1.119 0 0 1.119 0 2.5 C 0 3.709 0.859 4.718 2 4.95 L 2 9.05 C 0.859 9.282 0 10.291 0 11.5 C 0 12.881 1.119 14 2.5 14 C 3.881 14 5 12.881 5 11.5 C 5 10.291 4.141 9.282 3 9.05 L 3 8 L 7.5 8 C 8.881 8 10 6.881 10 5.5 L 10 4.95 C 11.141 4.718 12 3.709 12 2.5 C 12 1.119 10.881 0 9.5 0 C 8.119 0 7 1.119 7 2.5 C 7 3.709 7.859 4.718 9 4.95 L 9 5.5 C 9 6.328 8.328 7 7.5 7 L 3 7 L 3 4.95 Z M 2.5 4 C 1.672 4 1 3.328 1 2.5 C 1 1.672 1.672 1 2.5 1 C 3.328 1 4 1.672 4 2.5 C 4 3.328 3.328 4 2.5 4 Z M 9.5 4 C 8.672 4 8 3.328 8 2.5 C 8 1.672 8.672 1 9.5 1 C 10.328 1 11 1.672 11 2.5 C 11 3.328 10.328 4 9.5 4 Z M 4 11.5 C 4 12.328 3.328 13 2.5 13 C 1.672 13 1 12.328 1 11.5 C 1 10.672 1.672 10 2.5 10 C 3.328 10 4 10.672 4 11.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={14} viewBox="0 0 12 14" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 1,
        width: 12,
        height: 14,
      }}>
        <path d={"M 5 2.5 C 5 3.709 4.141 4.718 3 4.95 L 3 7 L 7.5 7 C 8.328 7 9 6.328 9 5.5 L 9 4.95 C 7.859 4.718 7 3.709 7 2.5 C 7 1.119 8.119 0 9.5 0 C 10.881 0 12 1.119 12 2.5 C 12 3.709 11.141 4.718 10 4.95 L 10 5.5 C 10 6.881 8.881 8 7.5 8 L 3 8 L 3 9.05 C 4.141 9.282 5 10.291 5 11.5 C 5 12.881 3.881 14 2.5 14 C 1.119 14 0 12.881 0 11.5 C 0 10.291 0.859 9.282 2 9.05 L 2 4.95 C 0.859 4.718 0 3.709 0 2.5 C 0 1.119 1.119 0 2.5 0 C 3.881 0 5 1.119 5 2.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14} height={16} viewBox="0 0 14 16" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 2,
        width: 14,
        height: 16,
      }}>
        <path d={"M 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.486 1.081 5.72 2.5 5.959 L 2.5 10.041 C 1.081 10.28 0 11.514 0 13 C 0 14.657 1.343 16 3 16 C 4.657 16 6 14.657 6 13 C 6 11.514 4.919 10.28 3.5 10.041 L 3.5 9 L 9 9 C 10.381 9 11.5 7.881 11.5 6.5 L 11.5 5.959 C 12.919 5.72 14 4.486 14 3 C 14 1.343 12.657 0 11 0 C 9.343 0 8 1.343 8 3 C 8 4.486 9.081 5.72 10.5 5.959 L 10.5 6.5 C 10.5 7.328 9.828 8 9 8 L 3.5 8 L 3.5 5.959 C 4.919 5.72 6 4.486 6 3 Z M 3 5 C 1.895 5 1 4.105 1 3 C 1 1.895 1.895 1 3 1 C 4.105 1 5 1.895 5 3 C 5 4.105 4.105 5 3 5 Z M 3 15 C 1.895 15 1 14.105 1 13 C 1 11.895 1.895 11 3 11 C 4.105 11 5 11.895 5 13 C 5 14.105 4.105 15 3 15 Z M 13 3 C 13 4.105 12.105 5 11 5 C 9.895 5 9 4.105 9 3 C 9 1.895 9.895 1 11 1 C 12.105 1 13 1.895 13 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14} height={16} viewBox="0 0 14 16" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 2,
        width: 14,
        height: 16,
      }}>
        <path d={"M 6 3 C 6 4.486 4.919 5.72 3.5 5.959 L 3.5 8 L 9 8 C 9.828 8 10.5 7.328 10.5 6.5 L 10.5 5.959 C 9.081 5.72 8 4.486 8 3 C 8 1.343 9.343 0 11 0 C 12.657 0 14 1.343 14 3 C 14 4.486 12.919 5.72 11.5 5.959 L 11.5 6.5 C 11.5 7.881 10.381 9 9 9 L 3.5 9 L 3.5 10.041 C 4.919 10.28 6 11.514 6 13 C 6 14.657 4.657 16 3 16 C 1.343 16 0 14.657 0 13 C 0 11.514 1.081 10.28 2.5 10.041 L 2.5 5.959 C 1.081 5.72 0 4.486 0 3 C 0 1.343 1.343 0 3 0 C 4.657 0 6 1.343 6 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={17} height={20} viewBox="0 0 17 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 17,
        height: 20,
      }}>
        <path d={"M 7 3.5 C 7 5.263 5.696 6.722 4 6.965 L 4 9.5 L 10.25 9.5 C 11.493 9.5 12.5 8.493 12.5 7.25 L 12.5 6.855 C 11.054 6.425 10 5.086 10 3.5 C 10 1.567 11.567 0 13.5 0 C 15.433 0 17 1.567 17 3.5 C 17 5.263 15.696 6.722 14 6.965 L 14 7.25 C 14 9.321 12.321 11 10.25 11 L 4 11 L 4 13.035 C 5.696 13.278 7 14.737 7 16.5 C 7 18.433 5.433 20 3.5 20 C 1.567 20 0 18.433 0 16.5 C 0 14.914 1.054 13.575 2.5 13.145 L 2.5 6.855 C 1.054 6.425 0 5.086 0 3.5 C 0 1.567 1.567 0 3.5 0 C 5.433 0 7 1.567 7 3.5 Z M 3.5 5.5 C 4.605 5.5 5.5 4.605 5.5 3.5 C 5.5 2.395 4.605 1.5 3.5 1.5 C 2.395 1.5 1.5 2.395 1.5 3.5 C 1.5 4.605 2.395 5.5 3.5 5.5 Z M 13.5 5.5 C 14.605 5.5 15.5 4.605 15.5 3.5 C 15.5 2.395 14.605 1.5 13.5 1.5 C 12.395 1.5 11.5 2.395 11.5 3.5 C 11.5 4.605 12.395 5.5 13.5 5.5 Z M 5.5 16.5 C 5.5 15.395 4.605 14.5 3.5 14.5 C 2.395 14.5 1.5 15.395 1.5 16.5 C 1.5 17.605 2.395 18.5 3.5 18.5 C 4.605 18.5 5.5 17.605 5.5 16.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={17} height={20} viewBox="0 0 17 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 17,
        height: 20,
      }}>
        <path d={"M 7 3.5 C 7 5.263 5.696 6.722 4 6.965 L 4 9.5 L 10.25 9.5 C 11.493 9.5 12.5 8.493 12.5 7.25 L 12.5 6.855 C 11.054 6.425 10 5.086 10 3.5 C 10 1.567 11.567 0 13.5 0 C 15.433 0 17 1.567 17 3.5 C 17 5.263 15.696 6.722 14 6.965 L 14 7.25 C 14 9.321 12.321 11 10.25 11 L 4 11 L 4 13.035 C 5.696 13.278 7 14.737 7 16.5 C 7 18.433 5.433 20 3.5 20 C 1.567 20 0 18.433 0 16.5 C 0 14.914 1.054 13.575 2.5 13.145 L 2.5 6.855 C 1.054 6.425 0 5.086 0 3.5 C 0 1.567 1.567 0 3.5 0 C 5.433 0 7 1.567 7 3.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={28} viewBox="0 0 24 28" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 24,
        height: 28,
      }}>
        <path d={"M 5 8 C 3.343 8 2 6.657 2 5 C 2 3.343 3.343 2 5 2 C 6.657 2 8 3.343 8 5 C 8 6.657 6.657 8 5 8 Z M 6 9.9 C 8.282 9.437 10 7.419 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.419 1.718 9.437 4 9.9 L 4 18.1 C 1.718 18.563 0 20.581 0 23 C 0 25.761 2.239 28 5 28 C 7.761 28 10 25.761 10 23 C 10 20.581 8.282 18.563 6 18.1 L 6 16 L 15 16 C 17.761 16 20 13.761 20 11 L 20 9.9 C 22.282 9.437 24 7.419 24 5 C 24 2.239 21.761 0 19 0 C 16.239 0 14 2.239 14 5 C 14 7.419 15.718 9.437 18 9.9 L 18 11 C 18 12.657 16.657 14 15 14 L 6 14 L 6 9.9 Z M 19 8 C 17.343 8 16 6.657 16 5 C 16 3.343 17.343 2 19 2 C 20.657 2 22 3.343 22 5 C 22 6.657 20.657 8 19 8 Z M 8 23 C 8 24.657 6.657 26 5 26 C 3.343 26 2 24.657 2 23 C 2 21.343 3.343 20 5 20 C 6.657 20 8 21.343 8 23 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={28} viewBox="0 0 24 28" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 24,
        height: 28,
      }}>
        <path d={"M 6 9.9 C 8.282 9.437 10 7.419 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.419 1.718 9.437 4 9.9 L 4 18.1 C 1.718 18.563 0 20.581 0 23 C 0 25.761 2.239 28 5 28 C 7.761 28 10 25.761 10 23 C 10 20.581 8.282 18.563 6 18.1 L 6 16 L 15 16 C 17.761 16 20 13.761 20 11 L 20 9.9 C 22.282 9.437 24 7.419 24 5 C 24 2.239 21.761 0 19 0 C 16.239 0 14 2.239 14 5 C 14 7.419 15.718 9.437 18 9.9 L 18 11 C 18 12.657 16.657 14 15 14 L 6 14 L 6 9.9 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body0,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body1,
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body2,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body3,
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body4,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body5,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body6,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body7,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default BranchFork;
