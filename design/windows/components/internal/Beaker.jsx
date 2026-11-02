import { __slateNorm } from './slateProps.jsx';
// figma node: 9000:214 Beaker (8 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Beaker(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24","32"],"theme":["regular","filled"]});
  const props = { ..._p, size: _p.size ?? "16", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14.144} height={18} viewBox="0 0 14.144 18" fill="none" style={{
        position: "absolute",
        left: 4.928,
        top: 3,
        width: 14.144,
        height: 18,
      }}>
        <path d={"M 3.072 1.5 L 4.072 1.5 L 4.072 7.738 C 4.072 8.113 3.978 8.482 3.799 8.812 L 0.214 15.415 C -0.419 16.581 0.426 18 1.752 18 L 12.391 18 C 13.718 18 14.562 16.581 13.929 15.415 L 10.344 8.812 C 10.165 8.482 10.072 8.113 10.072 7.738 L 10.072 1.5 L 11.072 1.5 C 11.486 1.5 11.822 1.164 11.822 0.75 C 11.822 0.336 11.486 0 11.072 0 L 3.072 0 C 2.658 0 2.322 0.336 2.322 0.75 C 2.322 1.164 2.658 1.5 3.072 1.5 Z M 5.572 7.738 L 5.572 1.5 L 8.572 1.5 L 8.572 7.738 C 8.572 8.363 8.728 8.978 9.026 9.527 L 9.826 11 L 4.318 11 L 5.117 9.527 C 5.416 8.978 5.572 8.363 5.572 7.738 Z M 3.504 12.5 L 10.64 12.5 L 12.611 16.131 C 12.701 16.297 12.581 16.5 12.391 16.5 L 1.752 16.5 C 1.563 16.5 1.442 16.297 1.533 16.131 L 3.504 12.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14.144} height={18} viewBox="0 0 14.144 18" fill="none" style={{
        position: "absolute",
        left: 4.928,
        top: 3,
        width: 14.144,
        height: 18,
      }}>
        <path d={"M 4.072 1.5 L 4.072 7.738 C 4.072 8.113 3.978 8.482 3.799 8.812 L 2.611 11 L 11.532 11 L 10.344 8.812 C 10.165 8.482 10.072 8.113 10.072 7.738 L 10.072 1.5 L 11.072 1.5 C 11.486 1.5 11.822 1.164 11.822 0.75 C 11.822 0.336 11.486 0 11.072 0 L 3.072 0 C 2.658 0 2.322 0.336 2.322 0.75 C 2.322 1.164 2.658 1.5 3.072 1.5 L 4.072 1.5 Z M 12.347 12.5 L 1.797 12.5 L 0.214 15.415 C -0.419 16.581 0.426 18 1.752 18 L 12.391 18 C 13.718 18 14.562 16.581 13.929 15.415 L 12.347 12.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.001} height={14.002} viewBox="0 0 12.001 14.002" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 3,
        width: 12.001,
        height: 14.002,
      }}>
        <path d={"M 10 0 C 10.276 0 10.5 0.224 10.5 0.5 C 10.5 0.776 10.276 1 10 1 L 9.004 1 L 9.004 5.684 C 9.004 6.263 9.148 6.834 9.423 7.344 L 11.819 11.79 C 12.357 12.789 11.634 14.002 10.499 14.002 L 1.502 14.002 C 0.366 14.002 -0.357 12.789 0.182 11.79 L 2.576 7.347 C 2.851 6.836 2.994 6.266 2.994 5.687 L 2.994 1 L 2 1 C 1.724 1 1.5 0.776 1.5 0.5 C 1.5 0.224 1.724 0 2 0 L 10 0 Z M 3.994 1 L 3.994 5.687 C 3.994 6.432 3.809 7.165 3.456 7.821 L 2.821 9 L 9.18 9 L 8.543 7.818 C 8.189 7.163 8.004 6.429 8.004 5.684 L 8.004 1 L 3.994 1 Z M 9.718 10 L 2.282 10 L 1.062 12.264 C 0.882 12.598 1.124 13.002 1.502 13.002 L 10.499 13.002 C 10.877 13.002 11.118 12.598 10.939 12.264 L 9.718 10 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.001} height={14.002} viewBox="0 0 12.001 14.002" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 3,
        width: 12.001,
        height: 14.002,
      }}>
        <path d={"M 1.5 0.5 C 1.5 0.224 1.724 0 2 0 L 10 0 C 10.276 0 10.5 0.224 10.5 0.5 C 10.5 0.776 10.276 1 10 1 L 9.004 1 L 9.004 5.684 C 9.004 6.263 9.148 6.834 9.423 7.344 L 10.316 9 L 1.685 9 L 2.576 7.347 C 2.851 6.836 2.994 6.266 2.994 5.687 L 2.994 1 L 2 1 C 1.724 1 1.5 0.776 1.5 0.5 Z M 1.146 10 L 0.182 11.79 C -0.357 12.789 0.366 14.002 1.502 14.002 L 10.499 14.002 C 11.634 14.002 12.357 12.789 11.819 11.79 L 10.854 10 L 1.146 10 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={9.522} height={12} viewBox="0 0 9.522 12" fill="none" style={{
        position: "absolute",
        left: 3.239,
        top: 2,
        width: 9.522,
        height: 12,
      }}>
        <path d={"M 2.261 1 L 2.761 1 L 2.761 4.689 C 2.761 5.132 2.644 5.566 2.421 5.949 L 0.207 9.744 C -0.377 10.744 0.345 12 1.502 12 L 8.02 12 C 9.178 12 9.899 10.744 9.316 9.744 L 7.102 5.949 C 6.879 5.566 6.761 5.132 6.761 4.689 L 6.761 1 L 7.261 1 C 7.537 1 7.761 0.776 7.761 0.5 C 7.761 0.224 7.537 0 7.261 0 L 2.261 0 C 1.985 0 1.761 0.224 1.761 0.5 C 1.761 0.776 1.985 1 2.261 1 Z M 3.761 4.689 L 3.761 1 L 5.761 1 L 5.761 4.689 C 5.761 5.309 5.926 5.917 6.238 6.453 L 6.557 7 L 2.965 7 L 3.284 6.453 C 3.597 5.917 3.761 5.309 3.761 4.689 Z M 2.382 8 L 7.141 8 L 8.452 10.248 C 8.647 10.581 8.406 11 8.02 11 L 1.502 11 C 1.116 11 0.876 10.581 1.07 10.248 L 2.382 8 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={9.522} height={12} viewBox="0 0 9.522 12" fill="none" style={{
        position: "absolute",
        left: 3.239,
        top: 2,
        width: 9.522,
        height: 12,
      }}>
        <path d={"M 2.761 1 L 2.761 4.689 C 2.761 5.132 2.644 5.566 2.421 5.949 L 1.807 7 L 7.715 7 L 7.102 5.949 C 6.879 5.566 6.761 5.132 6.761 4.689 L 6.761 1 L 7.261 1 C 7.537 1 7.761 0.776 7.761 0.5 C 7.761 0.224 7.537 0 7.261 0 L 2.261 0 C 1.985 0 1.761 0.224 1.761 0.5 C 1.761 0.776 1.985 1 2.261 1 L 2.761 1 Z M 8.298 8 L 1.224 8 L 0.207 9.744 C -0.377 10.744 0.345 12 1.502 12 L 8.02 12 C 9.178 12 9.899 10.744 9.316 9.744 L 8.298 8 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={21.995} height={26} viewBox="0 0 21.995 26" fill="none" style={{
        position: "absolute",
        left: 5.002,
        top: 3,
        width: 21.995,
        height: 26,
      }}>
        <path d={"M 6.998 2 L 6.998 10.438 C 6.998 11.252 6.749 12.046 6.286 12.715 L 4.704 15 L 17.291 15 L 15.709 12.715 C 15.246 12.046 14.998 11.252 14.998 10.438 L 14.998 2 L 15.998 2 C 16.55 2 16.998 1.552 16.998 1 C 16.998 0.448 16.55 0 15.998 0 L 5.998 0 C 5.446 0 4.998 0.448 4.998 1 C 4.998 1.552 5.446 2 5.998 2 L 6.998 2 Z M 18.675 17 L 3.32 17 L 0.576 20.963 C -0.898 23.092 0.626 26 3.216 26 L 18.78 26 C 21.369 26 22.893 23.092 21.419 20.963 L 18.675 17 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={21.995} height={26} viewBox="0 0 21.995 26" fill="none" style={{
        position: "absolute",
        left: 5.002,
        top: 3,
        width: 21.995,
        height: 26,
      }}>
        <path d={"M 14.998 2 L 14.998 10.438 C 14.998 11.252 15.246 12.046 15.709 12.715 L 21.419 20.963 C 22.893 23.092 21.369 26 18.78 26 L 3.216 26 C 0.626 26 -0.898 23.092 0.576 20.963 L 6.286 12.715 C 6.749 12.046 6.998 11.252 6.998 10.438 L 6.998 2 L 5.998 2 C 5.446 2 4.998 1.552 4.998 1 C 4.998 0.448 5.446 0 5.998 0 L 15.998 0 C 16.55 0 16.998 0.448 16.998 1 C 16.998 1.552 16.55 2 15.998 2 L 14.998 2 Z M 8.998 10.438 C 8.998 11.659 8.625 12.85 7.931 13.853 L 7.137 15 L 14.858 15 L 14.064 13.853 C 13.37 12.85 12.998 11.659 12.998 10.438 L 12.998 2 L 8.998 2 L 8.998 10.438 Z M 5.77 16.974 L 2.221 22.101 C 1.665 22.904 2.239 24 3.216 24 L 18.78 24 C 19.756 24 20.33 22.904 19.775 22.101 L 16.225 16.974 C 16.152 16.991 16.076 17 15.998 17 L 5.998 17 C 5.92 17 5.843 16.991 5.77 16.974 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body0,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body1,
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body2,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body3,
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body4,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body5,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body6,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body7,
  };
  return (__impls[__vkey(props)] ?? __body4)();
}
export default Beaker;
