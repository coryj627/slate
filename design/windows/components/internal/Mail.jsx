import { __slateNorm } from './slateProps.jsx';
// figma node: 9121:7488 Mail (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Mail(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","28","32","48"],"theme":["regular","filled","light"]});
  const props = { ..._p, size: _p.size ?? "12", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={40} height={32} viewBox="0 0 40 32" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 8,
        width: 40,
        height: 32,
      }}>
        <path d={"M 6.25 0 C 2.798 0 0 2.798 0 6.25 L 0 25.75 C 0 29.202 2.798 32 6.25 32 L 33.75 32 C 37.202 32 40 29.202 40 25.75 L 40 6.25 C 40 2.798 37.202 0 33.75 0 L 6.25 0 Z M 2.5 6.25 C 2.5 4.179 4.179 2.5 6.25 2.5 L 33.75 2.5 C 35.821 2.5 37.5 4.179 37.5 6.25 L 37.5 7.14 L 20.002 16.35 L 2.5 7.064 L 2.5 6.25 Z M 2.5 9.894 L 19.414 18.868 C 19.779 19.061 20.216 19.062 20.582 18.87 L 37.5 9.966 L 37.5 25.75 C 37.5 27.821 35.821 29.5 33.75 29.5 L 6.25 29.5 C 4.179 29.5 2.5 27.821 2.5 25.75 L 2.5 9.894 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={40} height={32} viewBox="0 0 40 32" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 8,
        width: 40,
        height: 32,
      }}>
        <path d={"M 0.02 5.747 C 0.276 2.53 2.968 0 6.25 0 L 33.75 0 C 37.06 0 39.769 2.573 39.986 5.828 L 20.002 16.35 L 0.02 5.747 Z M 0 8.567 L 0 25.75 C 0 29.202 2.798 32 6.25 32 L 33.75 32 C 37.202 32 40 29.202 40 25.75 L 40 8.646 L 20.582 18.87 C 20.217 19.062 19.779 19.061 19.414 18.868 L 0 8.567 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={23.997} height={19} viewBox="0 0 23.997 19" fill="none" style={{
        position: "absolute",
        left: 2.004,
        top: 4,
        width: 23.997,
        height: 19,
      }}>
        <path d={"M 3.75 0 C 1.679 0 0 1.679 0 3.75 L 0 15.25 C 0 17.321 1.679 19 3.75 19 L 20.247 19 C 22.318 19 23.997 17.321 23.997 15.25 L 23.997 3.75 C 23.997 1.679 22.318 0 20.247 0 L 3.75 0 Z M 1.5 3.75 C 1.5 2.507 2.507 1.5 3.75 1.5 L 20.247 1.5 C 21.489 1.5 22.497 2.507 22.497 3.75 L 22.497 4.529 L 11.996 9.907 L 1.5 4.531 L 1.5 3.75 Z M 1.5 6.217 L 11.654 11.418 C 11.869 11.527 12.123 11.527 12.338 11.418 L 22.497 6.214 L 22.497 15.25 C 22.497 16.493 21.489 17.5 20.247 17.5 L 3.75 17.5 C 2.507 17.5 1.5 16.493 1.5 15.25 L 1.5 6.217 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={23.997} height={19} viewBox="0 0 23.997 19" fill="none" style={{
        position: "absolute",
        left: 2.004,
        top: 4,
        width: 23.997,
        height: 19,
      }}>
        <path d={"M 0 3.75 C 0 1.679 1.679 0 3.75 0 L 20.247 0 C 22.318 0 23.997 1.679 23.997 3.75 L 23.997 3.761 L 11.996 9.907 L 0 3.764 L 0 3.75 Z M 0 5.449 L 0 15.25 C 0 17.321 1.679 19 3.75 19 L 20.247 19 C 22.318 19 23.997 17.321 23.997 15.25 L 23.997 5.446 L 12.338 11.418 C 12.123 11.527 11.869 11.527 11.654 11.418 L 0 5.449 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={16} viewBox="0 0 20 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 4,
        width: 20,
        height: 16,
      }}>
        <path d={"M 3.25 0 L 16.75 0 C 18.483 0 19.899 1.356 19.995 3.066 L 20 3.25 L 20 12.75 C 20 14.483 18.644 15.899 16.934 15.995 L 16.75 16 L 3.25 16 C 1.517 16 0.101 14.644 0.005 12.934 L 0 12.75 L 0 3.25 C 0 1.517 1.356 0.101 3.066 0.005 L 3.25 0 L 16.75 0 L 3.25 0 Z M 18.5 5.373 L 10.349 9.664 C 10.162 9.762 9.943 9.776 9.747 9.706 L 9.651 9.664 L 1.5 5.374 L 1.5 12.75 C 1.5 13.668 2.207 14.421 3.106 14.494 L 3.25 14.5 L 16.75 14.5 C 17.668 14.5 18.421 13.793 18.494 12.894 L 18.5 12.75 L 18.5 5.373 Z M 16.75 1.5 L 3.25 1.5 C 2.332 1.5 1.579 2.207 1.506 3.106 L 1.5 3.25 L 1.5 3.679 L 10 8.152 L 18.5 3.678 L 18.5 3.25 C 18.5 2.332 17.793 1.579 16.894 1.506 L 16.75 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={16} viewBox="0 0 20 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 4,
        width: 20,
        height: 16,
      }}>
        <path d={"M 20 4.608 L 20 12.75 C 20 14.483 18.644 15.899 16.934 15.995 L 16.75 16 L 3.25 16 C 1.517 16 0.101 14.644 0.005 12.934 L 0 12.75 L 0 4.608 L 9.652 9.664 C 9.87 9.779 10.13 9.779 10.348 9.664 L 20 4.608 Z M 3.25 0 L 16.75 0 C 18.435 0 19.82 1.282 19.984 2.924 L 10 8.153 L 0.016 2.924 C 0.174 1.343 1.464 0.095 3.064 0.005 L 3.25 0 L 16.75 0 L 3.25 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={13} viewBox="0 0 16 13" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 4,
        width: 16,
        height: 13,
      }}>
        <path d={"M 13.5 0 C 14.881 0 16 1.119 16 2.5 L 16 10.5 C 16 11.881 14.881 13 13.5 13 L 2.5 13 C 1.119 13 0 11.881 0 10.5 L 0 2.5 C 0 1.119 1.119 0 2.5 0 L 13.5 0 Z M 15 3.961 L 8.254 7.931 C 8.123 8.008 7.967 8.02 7.828 7.969 L 7.746 7.931 L 1 3.963 L 1 10.5 C 1 11.328 1.672 12 2.5 12 L 13.5 12 C 14.328 12 15 11.328 15 10.5 L 15 3.961 Z M 13.5 1 L 2.5 1 C 1.672 1 1 1.672 1 2.5 L 1 2.802 L 8 6.92 L 15 2.801 L 15 2.5 C 15 1.672 14.328 1 13.5 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={13} viewBox="0 0 16 13" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 4,
        width: 16,
        height: 13,
      }}>
        <path d={"M 16 3.373 L 16 10.5 C 16 11.881 14.881 13 13.5 13 L 2.5 13 C 1.119 13 0 11.881 0 10.5 L 0 3.373 L 7.747 7.931 C 7.903 8.023 8.097 8.023 8.254 7.931 L 16 3.373 Z M 13.5 0 C 14.787 0 15.847 0.973 15.985 2.223 L 8 6.92 L 0.015 2.223 C 0.153 0.973 1.213 0 2.5 0 L 13.5 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={10} viewBox="0 0 12 10" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 12,
        height: 10,
      }}>
        <path d={"M 0 3.038 L 0 8 C 0 9.105 0.895 10 2 10 L 10 10 C 11.105 10 12 9.105 12 8 L 12 2 C 12 0.895 11.105 0 10 0 L 2 0 C 0.895 0 0 0.895 0 2 L 0 3.038 C 0 3.038 0 3.038 0 3.038 Z M 2 1 L 10 1 C 10.552 1 11 1.448 11 2 L 11 2.74 L 6 5.432 L 1 2.74 L 1 2 C 1 1.448 1.448 1 2 1 Z M 1 3.876 L 5.763 6.44 C 5.911 6.52 6.089 6.52 6.237 6.44 L 11 3.876 L 11 8 C 11 8.552 10.552 9 10 9 L 2 9 C 1.448 9 1 8.552 1 8 L 1 3.876 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={10} viewBox="0 0 12 10" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 12,
        height: 10,
      }}>
        <path d={"M 2 0 C 0.895 0 0 0.895 0 2 L 0 2.201 L 6 5.432 L 12 2.201 L 12 2 C 12 0.895 11.105 0 10 0 L 2 0 Z M 12 3.337 L 6.237 6.44 C 6.089 6.52 5.911 6.52 5.763 6.44 L 0 3.337 L 0 8 C 0 9.105 0.895 10 2 10 L 10 10 C 11.105 10 12 9.105 12 8 L 12 3.337 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 8,
        height: 6,
      }}>
        <path d={"M 1.5 0 C 0.672 0 0 0.672 0 1.5 L 0 4.5 C 0 5.328 0.672 6 1.5 6 L 6.5 6 C 7.328 6 8 5.328 8 4.5 L 8 1.5 C 8 0.672 7.328 0 6.5 0 L 1.5 0 Z M 1 1.5 C 1 1.224 1.224 1 1.5 1 L 6.5 1 C 6.776 1 7 1.224 7 1.5 L 7 1.67 L 4 2.956 L 1 1.67 L 1 1.5 Z M 1 2.758 L 3.803 3.959 C 3.929 4.013 4.071 4.013 4.197 3.959 L 7 2.758 L 7 4.5 C 7 4.776 6.776 5 6.5 5 L 1.5 5 C 1.224 5 1 4.776 1 4.5 L 1 2.758 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8} height={6} viewBox="0 0 8 6" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 8,
        height: 6,
      }}>
        <path d={"M 0 2.248 L 0 4.5 C 0 5.328 0.672 6 1.5 6 L 6.5 6 C 7.328 6 8 5.328 8 4.5 L 8 2.248 L 4.205 3.956 C 4.074 4.015 3.925 4.015 3.794 3.956 L 0 2.248 Z M 0.037 1.168 L 4 2.952 L 7.963 1.168 C 7.812 0.499 7.214 0 6.5 0 L 1.5 0 C 0.786 0 0.188 0.499 0.037 1.168 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28.000} height={22} viewBox="0 0 28.000 22" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 5,
        width: 28,
        height: 22,
      }}>
        <path d={"M 4.5 0 C 2.015 0 0 2.015 0 4.5 L 0 5.979 C 0 5.993 0 6.006 0 6.019 L 0 17.5 C 0 19.985 2.015 22 4.5 22 L 23.5 22 C 25.985 22 28 19.985 28 17.5 L 28 6.019 C 28 6.006 28 5.993 28 5.979 L 28 4.5 C 28 2.015 25.985 0 23.5 0 L 4.5 0 Z M 26 5.403 L 14 11.864 L 2 5.403 L 2 4.5 C 2 3.119 3.119 2 4.5 2 L 23.5 2 C 24.881 2 26 3.119 26 4.5 L 26 5.403 Z M 2 7.674 L 13.526 13.88 C 13.822 14.04 14.178 14.04 14.474 13.88 L 26 7.674 L 26 17.5 C 26 18.881 24.881 20 23.5 20 L 4.5 20 C 3.119 20 2 18.881 2 17.5 L 2 7.674 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={22} viewBox="0 0 28 22" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 5,
        width: 28,
        height: 22,
      }}>
        <path d={"M 0.004 4.303 C 0.107 1.909 2.081 0 4.5 0 L 23.5 0 C 25.919 0 27.893 1.909 27.996 4.303 L 26.52 5.123 L 14 11.864 L 1.48 5.123 L 0.004 4.303 Z M 0 6.588 L 0 17.5 C 0 19.985 2.015 22 4.5 22 L 23.5 22 C 25.985 22 28 19.985 28 17.5 L 28 6.588 L 27.474 6.881 L 14.474 13.88 C 14.178 14.04 13.822 14.04 13.526 13.88 L 0.514 6.874 L 0 6.588 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28.000} height={22} viewBox="0 0 28.000 22" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 5,
        width: 28,
        height: 22,
      }}>
        <path d={"M 4.5 0 L 23.5 0 C 25.985 0 28 2.015 28 4.5 L 28 5.49 C 28 5.496 28 5.503 28 5.509 L 28 17.5 C 28 19.985 25.985 22 23.5 22 L 4.5 22 C 2.015 22 0 19.985 0 17.5 L 0 5.509 C 0 5.503 0 5.496 0 5.49 L 0 4.5 C 0 2.015 2.015 0 4.5 0 Z M 27 5.206 L 27 4.5 C 27 2.567 25.433 1 23.5 1 L 4.5 1 C 2.567 1 1 2.567 1 4.5 L 1 5.206 L 14 12.428 L 27 5.206 Z M 1 6.35 L 1 17.5 C 1 19.433 2.567 21 4.5 21 L 23.5 21 C 25.433 21 27 19.433 27 17.5 L 27 6.35 L 14.243 13.437 C 14.092 13.521 13.908 13.521 13.757 13.437 L 1 6.35 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body0,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body1,
    // figma: Size=28, Theme=Regular
    "size=28|theme=regular": __body2,
    // figma: Size=28, Theme=Filled
    "size=28|theme=filled": __body3,
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body4,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body5,
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body6,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body7,
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body8,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body9,
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body10,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body11,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body12,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body13,
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body14,
  };
  return (__impls[__vkey(props)] ?? __body10)();
}
export default Mail;
