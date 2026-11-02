import { __slateNorm } from './slateProps.jsx';
// figma node: 9240:8980 Image Circle (12 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function ImageCircle2(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24","28","32","48"],"theme":["regular","filled"]});
  const props = { ..._p, size: _p.size ?? "16", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 40,
        height: 40,
      }}>
        <path d={"M 20 2.5 C 10.335 2.5 2.5 10.335 2.5 20 C 2.5 24.449 4.16 28.511 6.895 31.599 L 17.532 21.019 C 18.897 19.66 21.103 19.66 22.468 21.019 L 33.105 31.599 C 35.84 28.511 37.5 24.449 37.5 20 C 37.5 10.335 29.665 2.5 20 2.5 Z M 31.318 33.348 L 20.705 22.791 C 20.315 22.403 19.685 22.403 19.295 22.791 L 8.682 33.348 C 11.733 35.938 15.684 37.5 20 37.5 C 24.316 37.5 28.267 35.938 31.318 33.348 Z M 0 20 C 0 8.954 8.954 0 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 Z M 26.5 15.5 C 25.395 15.5 24.5 14.605 24.5 13.5 C 24.5 12.395 25.395 11.5 26.5 11.5 C 27.605 11.5 28.5 12.395 28.5 13.5 C 28.5 14.605 27.605 15.5 26.5 15.5 Z M 26.5 18 C 28.985 18 31 15.985 31 13.5 C 31 11.015 28.985 9 26.5 9 C 24.015 9 22 11.015 22 13.5 C 22 15.985 24.015 18 26.5 18 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 40,
        height: 40,
      }}>
        <path d={"M 33.096 35.116 C 29.587 38.159 25.009 40 20 40 C 14.991 40 10.413 38.159 6.904 35.116 L 19.295 22.791 C 19.685 22.403 20.315 22.403 20.705 22.791 L 33.096 35.116 Z M 34.88 33.364 L 22.468 21.019 C 21.103 19.66 18.897 19.66 17.532 21.019 L 5.12 33.364 C 1.937 29.822 0 25.137 0 20 C 0 8.954 8.954 0 20 0 C 31.046 0 40 8.954 40 20 C 40 25.137 38.063 29.822 34.88 33.364 Z M 31 13 C 31 10.791 29.209 9 27 9 C 24.791 9 23 10.791 23 13 C 23 15.209 24.791 17 27 17 C 29.209 17 31 15.209 31 13 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 24,
        height: 24,
      }}>
        <path d={"M 12 1.5 C 17.799 1.5 22.5 6.201 22.5 12 C 22.5 14.645 21.522 17.061 19.909 18.907 L 13.396 12.557 C 12.619 11.799 11.38 11.799 10.604 12.557 L 4.091 18.907 C 2.478 17.061 1.5 14.645 1.5 12 C 1.5 6.201 6.201 1.5 12 1.5 Z M 20.576 20.393 C 22.694 18.229 24 15.267 24 12 C 24 5.373 18.627 0 12 0 C 5.373 0 0 5.373 0 12 C 0 15.267 1.306 18.229 3.423 20.393 C 3.451 20.435 3.482 20.476 3.519 20.513 C 3.561 20.556 3.607 20.593 3.656 20.624 C 5.815 22.714 8.757 24 12 24 C 15.243 24 18.185 22.714 20.345 20.624 C 20.393 20.593 20.439 20.556 20.481 20.513 C 20.518 20.476 20.549 20.436 20.576 20.393 Z M 18.844 19.964 C 17.006 21.544 14.614 22.5 12 22.5 C 9.386 22.5 6.994 21.544 5.156 19.964 L 11.651 13.631 C 11.845 13.441 12.155 13.441 12.349 13.63 L 18.844 19.964 Z M 16 9 C 15.448 9 15 8.552 15 8 C 15 7.448 15.448 7 16 7 C 16.552 7 17 7.448 17 8 C 17 8.552 16.552 9 16 9 Z M 16 10.5 C 17.381 10.5 18.5 9.381 18.5 8 C 18.5 6.619 17.381 5.5 16 5.5 C 14.619 5.5 13.5 6.619 13.5 8 C 13.5 9.381 14.619 10.5 16 10.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 24,
        height: 24,
      }}>
        <path d={"M 12 0 C 5.373 0 0 5.373 0 12 C 0 15.052 1.139 17.838 3.016 19.956 L 10.604 12.557 C 11.38 11.799 12.62 11.799 13.396 12.557 L 20.984 19.956 C 22.861 17.838 24 15.052 24 12 C 24 5.373 18.627 0 12 0 Z M 19.921 21.014 L 12.349 13.63 C 12.155 13.441 11.845 13.441 11.651 13.631 L 4.079 21.014 C 6.192 22.873 8.964 24 12 24 C 15.036 24 17.808 22.873 19.921 21.014 Z M 18.5 7.75 C 18.5 8.993 17.493 10 16.25 10 C 15.007 10 14 8.993 14 7.75 C 14 6.507 15.007 5.5 16.25 5.5 C 17.493 5.5 18.5 6.507 18.5 7.75 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 20,
        height: 20,
      }}>
        <path d={"M 10 1.5 C 5.306 1.5 1.5 5.306 1.5 10 C 1.5 12.081 2.248 13.987 3.489 15.465 L 8.424 10.622 C 9.299 9.763 10.701 9.763 11.576 10.622 L 16.511 15.465 C 17.752 13.987 18.5 12.081 18.5 10 C 18.5 5.306 14.694 1.5 10 1.5 Z M 15.449 16.524 L 10.525 11.692 C 10.234 11.406 9.766 11.406 9.475 11.692 L 4.551 16.524 C 6.026 17.758 7.927 18.5 10 18.5 C 12.073 18.5 13.974 17.758 15.449 16.524 Z M 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 Z M 13.25 7.5 C 12.836 7.5 12.5 7.164 12.5 6.75 C 12.5 6.336 12.836 6 13.25 6 C 13.664 6 14 6.336 14 6.75 C 14 7.164 13.664 7.5 13.25 7.5 Z M 13.25 9 C 14.493 9 15.5 7.993 15.5 6.75 C 15.5 5.507 14.493 4.5 13.25 4.5 C 12.007 4.5 11 5.507 11 6.75 C 11 7.993 12.007 9 13.25 9 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 20,
        height: 20,
      }}>
        <path d={"M 17.564 16.542 C 19.082 14.788 20 12.501 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 12.501 0.918 14.788 2.436 16.542 L 8.419 10.633 C 9.295 9.768 10.705 9.768 11.581 10.633 L 17.564 16.542 Z M 16.5 17.599 L 10.527 11.701 C 10.235 11.412 9.765 11.412 9.473 11.701 L 3.5 17.599 C 5.248 19.096 7.518 20 10 20 C 12.482 20 14.752 19.096 16.5 17.599 Z M 15.5 6.5 C 15.5 7.605 14.605 8.5 13.5 8.5 C 12.395 8.5 11.5 7.605 11.5 6.5 C 11.5 5.395 12.395 4.5 13.5 4.5 C 14.605 4.5 15.5 5.395 15.5 6.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
      }}>
        <path d={"M 8 1 C 4.134 1 1 4.134 1 8 C 1 9.755 1.646 11.359 2.712 12.587 L 6.948 8.419 C 7.532 7.844 8.468 7.844 9.052 8.419 L 13.288 12.587 C 14.354 11.359 15 9.755 15 8 C 15 4.134 11.866 1 8 1 Z M 8 15 C 9.751 15 11.352 14.357 12.58 13.294 L 8.351 9.131 C 8.156 8.94 7.844 8.94 7.649 9.131 L 3.42 13.294 C 4.648 14.357 6.249 15 8 15 Z M 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 Z M 11 5.5 C 11 5.224 10.776 5 10.5 5 C 10.224 5 10 5.224 10 5.5 C 10 5.776 10.224 6 10.5 6 C 10.776 6 11 5.776 11 5.5 Z M 12 5.5 C 12 6.328 11.328 7 10.5 7 C 9.672 7 9 6.328 9 5.5 C 9 4.672 9.672 4 10.5 4 C 11.328 4 12 4.672 12 5.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
      }}>
        <path d={"M 16 8 C 16 10.028 15.245 11.88 14.002 13.29 L 9.053 8.419 C 8.469 7.844 7.532 7.844 6.948 8.419 L 1.999 13.29 C 0.755 11.88 0 10.028 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 Z M 8.351 9.131 L 13.295 13.997 C 11.884 15.244 10.03 16 8 16 C 5.97 16 4.116 15.244 2.705 13.997 L 7.65 9.131 C 7.844 8.94 8.156 8.94 8.351 9.131 Z M 10.75 6.5 C 11.44 6.5 12 5.94 12 5.25 C 12 4.56 11.44 4 10.75 4 C 10.06 4 9.5 4.56 9.5 5.25 C 9.5 5.94 10.06 6.5 10.75 6.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 12,
        height: 12,
      }}>
        <path d={"M 6 1 C 3.239 1 1 3.239 1 6 C 1 7.208 1.429 8.316 2.142 9.181 L 4.948 6.419 C 5.532 5.844 6.469 5.844 7.053 6.419 L 9.859 9.18 C 10.572 8.316 11 7.208 11 6 C 11 3.239 8.761 1 6 1 Z M 6 11 C 7.193 11 8.289 10.582 9.148 9.884 L 6.351 7.131 C 6.157 6.94 5.844 6.94 5.65 7.131 L 2.852 9.885 C 3.711 10.582 4.807 11 6 11 Z M 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 Z M 8 5 C 8.552 5 9 4.552 9 4 C 9 3.448 8.552 3 8 3 C 7.448 3 7 3.448 7 4 C 7 4.552 7.448 5 8 5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 12,
        height: 12,
      }}>
        <path d={"M 12 6 C 12 7.481 11.463 8.837 10.573 9.884 L 7.052 6.419 C 6.468 5.844 5.532 5.844 4.948 6.419 L 1.427 9.884 C 0.537 8.837 0 7.481 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 Z M 6.351 7.131 L 9.865 10.59 C 8.821 11.47 7.472 12 6 12 C 4.528 12 3.179 11.47 2.135 10.59 L 5.649 7.131 C 5.844 6.94 6.156 6.94 6.351 7.131 Z M 8 5 C 8.552 5 9 4.552 9 4 C 9 3.448 8.552 3 8 3 C 7.448 3 7 3.448 7 4 C 7 4.552 7.448 5 8 5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28,
      }}>
        <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.924 3.046 19.604 4.784 21.686 L 12.23 14.233 C 13.207 13.256 14.791 13.256 15.767 14.233 L 23.215 21.687 C 24.954 19.605 26 16.925 26 14 C 26 7.373 20.627 2 14 2 Z M 21.81 23.111 L 14.353 15.647 C 14.157 15.451 13.84 15.451 13.645 15.647 L 6.189 23.11 C 8.288 24.911 11.017 26 14 26 C 16.983 26 19.711 24.912 21.81 23.111 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 18.5 11 C 17.672 11 17 10.328 17 9.5 C 17 8.672 17.672 8 18.5 8 C 19.328 8 20 8.672 20 9.5 C 20 10.328 19.328 11 18.5 11 Z M 18.5 13 C 20.433 13 22 11.433 22 9.5 C 22 7.567 20.433 6 18.5 6 C 16.567 6 15 7.567 15 9.5 C 15 11.433 16.567 13 18.5 13 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28,
      }}>
        <path d={"M 23.228 24.529 C 20.764 26.69 17.535 28 14 28 C 10.465 28 7.236 26.69 4.772 24.529 L 13.646 15.647 C 13.842 15.451 14.158 15.451 14.354 15.647 L 23.228 24.529 Z M 24.634 23.106 L 15.769 14.233 C 14.792 13.256 13.208 13.256 12.231 14.233 L 3.366 23.106 C 1.268 20.658 0 17.477 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 17.477 26.732 20.658 24.634 23.106 Z M 22 9.25 C 22 7.455 20.545 6 18.75 6 C 16.955 6 15.5 7.455 15.5 9.25 C 15.5 11.045 16.955 12.5 18.75 12.5 C 20.545 12.5 22 11.045 22 9.25 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body10,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body11,
  };
  return (__impls[__vkey(props)] ?? __body8)();
}
export default ImageCircle2;
