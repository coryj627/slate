import { __slateNorm } from './slateProps.jsx';
// figma node: 9000:401 Person (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Person(_p = {}) {
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
      <svg width={32} height={40} viewBox="0 0 32 40" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 4,
        width: 32,
        height: 40,
      }}>
        <path d={"M 16 0 C 10.477 0 6 4.477 6 10 C 6 15.523 10.477 20 16 20 C 21.523 20 26 15.523 26 10 C 26 4.477 21.523 0 16 0 Z M 8.5 10 C 8.5 5.858 11.858 2.5 16 2.5 C 20.142 2.5 23.5 5.858 23.5 10 C 23.5 14.142 20.142 17.5 16 17.5 C 11.858 17.5 8.5 14.142 8.5 10 Z M 4.25 24 C 1.903 24 0 25.901 0 28.249 L 0 29 C 0 32.756 1.942 35.567 4.92 37.38 C 7.849 39.163 11.786 40 16 40 C 20.214 40 24.151 39.163 27.08 37.38 C 30.058 35.567 32 32.756 32 29 L 32 28.249 C 32 25.901 30.097 24 27.75 24 L 4.25 24 Z M 2.5 28.249 C 2.5 27.283 3.283 26.5 4.25 26.5 L 27.75 26.5 C 28.717 26.5 29.5 27.283 29.5 28.249 L 29.5 29 C 29.5 31.744 28.14 33.808 25.78 35.245 C 23.37 36.712 19.932 37.5 16 37.5 C 12.068 37.5 8.63 36.712 6.22 35.245 C 3.86 33.808 2.5 31.744 2.5 29 L 2.5 28.249 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={32} height={40} viewBox="0 0 32 40" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 4,
        width: 32,
        height: 40,
      }}>
        <path d={"M 16 0 C 10.477 0 6 4.477 6 10 C 6 15.523 10.477 20 16 20 C 21.523 20 26 15.523 26 10 C 26 4.477 21.523 0 16 0 Z M 4.25 24 C 1.903 24 0 25.901 0 28.249 L 0 29 C 0 32.756 1.942 35.567 4.92 37.38 C 7.849 39.163 11.786 40 16 40 C 20.214 40 24.151 39.163 27.08 37.38 C 30.058 35.567 32 32.756 32 29 L 32 28.249 C 32 25.901 30.097 24 27.75 24 L 4.25 24 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={24} viewBox="0 0 20 24" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 20,
        height: 24,
      }}>
        <path d={"M 17 14 C 18.657 14 20 15.343 20 17 L 20 17.715 C 20 21.292 15.789 24 10 24 C 4.211 24 0 21.433 0 17.715 L 0 17 C 0 15.343 1.343 14 3 14 L 17 14 Z M 17 15.5 L 3 15.5 C 2.22 15.5 1.58 16.095 1.507 16.856 L 1.5 17 L 1.5 17.715 C 1.5 20.389 4.889 22.5 10 22.5 C 14.926 22.5 18.355 20.395 18.496 17.876 L 18.5 17.715 L 18.5 17 C 18.5 16.22 17.905 15.58 17.144 15.507 L 17 15.5 Z M 10 0 C 13.314 0 16 2.686 16 6 C 16 9.314 13.314 12 10 12 C 6.686 12 4 9.314 4 6 C 4 2.686 6.686 0 10 0 Z M 10 1.5 C 7.515 1.5 5.5 3.515 5.5 6 C 5.5 8.485 7.515 10.5 10 10.5 C 12.485 10.5 14.5 8.485 14.5 6 C 14.5 3.515 12.485 1.5 10 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={24} viewBox="0 0 20 24" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 20,
        height: 24,
      }}>
        <path d={"M 17 14 C 18.657 14 20 15.343 20 17 L 20 17.715 C 20 21.292 15.789 24 10 24 C 4.211 24 0 21.433 0 17.715 L 0 17 C 0 15.343 1.343 14 3 14 L 17 14 Z M 10 0 C 13.314 0 16 2.686 16 6 C 16 9.314 13.314 12 10 12 C 6.686 12 4 9.314 4 6 C 4 2.686 6.686 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16.000} height={19.996} viewBox="0 0 16.000 19.996" fill="none" style={{
        position: "absolute",
        left: 4.004,
        top: 2.005,
        width: 16,
        height: 19.996,
      }}>
        <path d={"M 13.751 11.995 C 14.993 11.995 16 13.002 16 14.244 L 16 14.82 C 16 15.714 15.68 16.579 15.098 17.258 C 13.529 19.092 11.142 19.996 7.996 19.996 C 4.851 19.996 2.465 19.091 0.898 17.257 C 0.319 16.578 0 15.715 0 14.822 L 0 14.244 C 0 13.002 1.007 11.995 2.249 11.995 L 13.751 11.995 Z M 13.751 13.495 L 2.249 13.495 C 1.835 13.495 1.5 13.831 1.5 14.244 L 1.5 14.822 C 1.5 15.358 1.691 15.876 2.039 16.283 C 3.292 17.751 5.258 18.496 7.996 18.496 C 10.735 18.496 12.702 17.751 13.959 16.283 C 14.308 15.875 14.5 15.356 14.5 14.82 L 14.5 14.244 C 14.5 13.831 14.164 13.495 13.751 13.495 Z M 7.996 0 C 10.758 0 12.996 2.239 12.996 5 C 12.996 7.761 10.758 10 7.996 10 C 5.235 10 2.996 7.761 2.996 5 C 2.996 2.239 5.235 0 7.996 0 Z M 7.996 1.5 C 6.063 1.5 4.496 3.067 4.496 5 C 4.496 6.933 6.063 8.5 7.996 8.5 C 9.929 8.5 11.496 6.933 11.496 5 C 11.496 3.067 9.929 1.5 7.996 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16.000} height={19.996} viewBox="0 0 16.000 19.996" fill="none" style={{
        position: "absolute",
        left: 4.004,
        top: 2.005,
        width: 16,
        height: 19.996,
      }}>
        <path d={"M 13.751 11.995 C 14.993 11.995 16 13.002 16 14.244 L 16 15.163 C 16 15.736 15.82 16.295 15.487 16.762 C 13.941 18.925 11.417 19.996 7.996 19.996 C 4.575 19.996 2.053 18.924 0.51 16.76 C 0.178 16.294 0 15.736 0 15.164 L 0 14.244 C 0 13.002 1.007 11.995 2.249 11.995 L 13.751 11.995 Z M 7.996 0 C 10.758 0 12.996 2.239 12.996 5 C 12.996 7.761 10.758 10 7.996 10 C 5.235 10 2.996 7.761 2.996 5 C 2.996 2.239 5.235 0 7.996 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14} height={16} viewBox="0 0 14 16" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 2,
        width: 14,
        height: 16,
      }}>
        <path d={"M 7 0 C 4.791 0 3 1.791 3 4 C 3 6.209 4.791 8 7 8 C 9.209 8 11 6.209 11 4 C 11 1.791 9.209 0 7 0 Z M 4 4 C 4 2.343 5.343 1 7 1 C 8.657 1 10 2.343 10 4 C 10 5.657 8.657 7 7 7 C 5.343 7 4 5.657 4 4 Z M 2.009 9 C 0.903 9 0 9.887 0 11 C 0 12.691 0.833 13.966 2.135 14.797 C 3.417 15.614 5.145 16 7 16 C 8.855 16 10.583 15.614 11.865 14.797 C 13.167 13.966 14 12.691 14 11 C 14 9.896 13.105 9 12 9 L 2.009 9 Z M 1 11 C 1 10.447 1.448 10 2.009 10 L 12 10 C 12.552 10 13 10.448 13 11 C 13 12.309 12.378 13.284 11.327 13.953 C 10.257 14.636 8.735 15 7 15 C 5.265 15 3.743 14.636 2.673 13.953 C 1.622 13.284 1 12.309 1 11 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14} height={16} viewBox="0 0 14 16" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 2,
        width: 14,
        height: 16,
      }}>
        <path d={"M 7 0 C 4.791 0 3 1.791 3 4 C 3 6.209 4.791 8 7 8 C 9.209 8 11 6.209 11 4 C 11 1.791 9.209 0 7 0 Z M 2.009 9 C 0.903 9 0 9.887 0 11 C 0 12.691 0.833 13.966 2.135 14.797 C 3.417 15.614 5.145 16 7 16 C 8.855 16 10.583 15.614 11.865 14.797 C 13.167 13.966 14 12.691 14 11 C 14 9.896 13.105 9 12 9 L 2.009 9 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={12.500} viewBox="0 0 10 12.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 1.5,
        width: 10,
        height: 12.5,
      }}>
        <path d={"M 8.5 6.5 C 9.328 6.5 10 7.172 10 8 L 10 8.5 C 10 10.471 8.14 12.5 5 12.5 C 1.86 12.5 0 10.471 0 8.5 L 0 8 C 0 7.172 0.672 6.5 1.5 6.5 L 8.5 6.5 Z M 8.5 7.5 L 1.5 7.5 C 1.224 7.5 1 7.724 1 8 L 1 8.5 C 1 9.938 2.432 11.5 5 11.5 C 7.568 11.5 9 9.938 9 8.5 L 9 8 C 9 7.724 8.776 7.5 8.5 7.5 Z M 5 0 C 6.519 0 7.75 1.231 7.75 2.75 C 7.75 4.269 6.519 5.5 5 5.5 C 3.481 5.5 2.25 4.269 2.25 2.75 C 2.25 1.231 3.481 0 5 0 Z M 5 1 C 4.034 1 3.25 1.784 3.25 2.75 C 3.25 3.716 4.034 4.5 5 4.5 C 5.966 4.5 6.75 3.716 6.75 2.75 C 6.75 1.784 5.966 1 5 1 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={12.500} viewBox="0 0 10 12.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 1.5,
        width: 10,
        height: 12.5,
      }}>
        <path d={"M 8.5 6.5 C 9.328 6.5 10 7.172 10 8 L 10 8.5 C 10 10.471 8.14 12.5 5 12.5 C 1.86 12.5 0 10.471 0 8.5 L 0 8 C 0 7.172 0.672 6.5 1.5 6.5 L 8.5 6.5 Z M 5 0 C 6.519 0 7.75 1.231 7.75 2.75 C 7.75 4.269 6.519 5.5 5 5.5 C 3.481 5.5 2.25 4.269 2.25 2.75 C 2.25 1.231 3.481 0 5 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8} height={10} viewBox="0 0 8 10" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 1,
        width: 8,
        height: 10,
      }}>
        <path d={"M 4 0 C 2.895 0 2 0.895 2 2 C 2 3.105 2.895 4 4 4 C 5.105 4 6 3.105 6 2 C 6 0.895 5.105 0 4 0 Z M 3 2 C 3 1.448 3.448 1 4 1 C 4.552 1 5 1.448 5 2 C 5 2.552 4.552 3 4 3 C 3.448 3 3 2.552 3 2 Z M 6.5 5 L 1.5 5 C 0.672 5 0 5.672 0 6.5 C 0 7.616 0.459 8.51 1.212 9.115 C 1.953 9.71 2.947 10 4 10 C 5.053 10 6.047 9.71 6.788 9.115 C 7.541 8.51 8 7.616 8 6.5 C 8 5.672 7.328 5 6.5 5 Z M 1.5 6 L 6.5 6 C 6.776 6 7 6.224 7 6.5 C 7 7.317 6.675 7.923 6.162 8.335 C 5.636 8.757 4.88 9 4 9 C 3.12 9 2.364 8.757 1.838 8.335 C 1.325 7.923 1 7.317 1 6.5 C 1 6.224 1.224 6 1.5 6 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8} height={10} viewBox="0 0 8 10" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 1,
        width: 8,
        height: 10,
      }}>
        <path d={"M 4 0 C 2.895 0 2 0.895 2 2 C 2 3.105 2.895 4 4 4 C 5.105 4 6 3.105 6 2 C 6 0.895 5.105 0 4 0 Z M 6.5 5 L 1.5 5 C 0.672 5 0 5.672 0 6.5 C 0 7.616 0.459 8.51 1.212 9.115 C 1.953 9.71 2.947 10 4 10 C 5.053 10 6.047 9.71 6.788 9.115 C 7.541 8.51 8 7.616 8 6.5 C 8 5.672 7.328 5 6.5 5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
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
        <path d={"M 19 7 C 19 10.866 15.866 14 12 14 C 8.134 14 5 10.866 5 7 C 5 3.134 8.134 0 12 0 C 15.866 0 19 3.134 19 7 Z M 17 7 C 17 4.239 14.761 2 12 2 C 9.239 2 7 4.239 7 7 C 7 9.761 9.239 12 12 12 C 14.761 12 17 9.761 17 7 Z M 3.5 16 C 1.567 16 0 17.567 0 19.5 L 0 20 C 0 22.394 1.523 24.417 3.685 25.793 C 5.859 27.177 8.802 28 12 28 C 15.198 28 18.141 27.177 20.315 25.793 C 22.477 24.417 24 22.394 24 20 L 24 19.5 C 24 17.567 22.433 16 20.5 16 L 3.5 16 Z M 2 19.5 C 2 18.672 2.672 18 3.5 18 L 20.5 18 C 21.328 18 22 18.672 22 19.5 L 22 20 C 22 21.473 21.06 22.949 19.241 24.106 C 17.434 25.256 14.877 26 12 26 C 9.123 26 6.566 25.256 4.759 24.106 C 2.94 22.949 2 21.473 2 20 L 2 19.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
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
        <path d={"M 12 14 C 15.866 14 19 10.866 19 7 C 19 3.134 15.866 0 12 0 C 8.134 0 5 3.134 5 7 C 5 10.866 8.134 14 12 14 Z M 3.5 16 C 1.567 16 0 17.567 0 19.5 L 0 20 C 0 22.394 1.523 24.417 3.685 25.793 C 5.859 27.177 8.802 28 12 28 C 15.198 28 18.141 27.177 20.315 25.793 C 22.477 24.417 24 22.394 24 20 L 24 19.5 C 24 17.567 22.433 16 20.5 16 L 3.5 16 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={24.000} height={28} viewBox="0 0 24.000 28" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 24,
        height: 28,
      }}>
        <path d={"M 12 0 C 8.134 0 5 3.134 5 7 C 5 10.866 8.134 14 12 14 C 15.866 14 19 10.866 19 7 C 19 3.134 15.866 0 12 0 Z M 6 7 C 6 3.686 8.686 1 12 1 C 15.314 1 18 3.686 18 7 C 18 10.314 15.314 13 12 13 C 8.686 13 6 10.314 6 7 Z M 3.5 16 C 1.567 16 0 17.567 0 19.5 L 0 20.167 C 0 22.316 2.766 28 12 28 C 21.234 28 24 22.316 24 20.167 L 24 19.5 C 24 17.567 22.433 16 20.5 16 L 3.5 16 Z M 1 19.5 C 1 18.119 2.119 17 3.5 17 L 20.5 17 C 21.881 17 23 18.119 23 19.5 L 23 20.167 C 23 21.684 20.766 27 12 27 C 3.234 27 1 21.684 1 20.167 L 1 19.5 Z"} fill="currentColor" fillRule="nonzero" />
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
export default Person;
