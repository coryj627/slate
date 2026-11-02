import { __slateNorm } from './slateProps.jsx';
// figma node: 9328:24778 Chat (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Chat(_p = {}) {
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
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 40,
        height: 40,
      }}>
        <path d={"M 20 2.5 C 10.335 2.5 2.5 10.335 2.5 20 C 2.5 23.188 3.351 26.173 4.838 28.745 C 5.007 29.038 5.051 29.387 4.958 29.713 L 2.842 37.155 L 10.282 35.039 C 10.608 34.946 10.957 34.99 11.25 35.159 C 13.823 36.648 16.81 37.5 20 37.5 C 29.665 37.5 37.5 29.665 37.5 20 C 37.5 10.335 29.665 2.5 20 2.5 Z M 0 20 C 0 8.954 8.954 0 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 16.55 40 13.302 39.126 10.466 37.586 L 2.231 39.928 C 0.912 40.303 -0.306 39.084 0.069 37.766 L 2.411 29.528 C 0.873 26.694 0 23.448 0 20 Z M 12 16.25 C 12 15.56 12.56 15 13.25 15 L 26.75 15 C 27.44 15 28 15.56 28 16.25 C 28 16.94 27.44 17.5 26.75 17.5 L 13.25 17.5 C 12.56 17.5 12 16.94 12 16.25 Z M 13.25 22.5 C 12.56 22.5 12 23.06 12 23.75 C 12 24.44 12.56 25 13.25 25 L 22.75 25 C 23.44 25 24 24.44 24 23.75 C 24 23.06 23.44 22.5 22.75 22.5 L 13.25 22.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 40,
        height: 40,
      }}>
        <path d={"M 0 20 C 0 8.954 8.954 0 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 16.55 40 13.302 39.126 10.466 37.586 L 2.231 39.928 C 0.912 40.303 -0.306 39.084 0.069 37.766 L 2.411 29.528 C 0.873 26.694 0 23.448 0 20 Z M 12 16.25 C 12 16.94 12.56 17.5 13.25 17.5 L 26.75 17.5 C 27.44 17.5 28 16.94 28 16.25 C 28 15.56 27.44 15 26.75 15 L 13.25 15 C 12.56 15 12 15.56 12 16.25 Z M 13.25 22.5 C 12.56 22.5 12 23.06 12 23.75 C 12 24.44 12.56 25 13.25 25 L 22.75 25 C 23.44 25 24 24.44 24 23.75 C 24 23.06 23.44 22.5 22.75 22.5 L 13.25 22.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={28.002} viewBox="0 0 28 28.002" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28.002,
      }}>
        <path d={"M 14 2 C 7.373 2 2 7.373 2 14 C 2 16.165 2.572 18.193 3.573 19.945 C 3.707 20.178 3.741 20.456 3.667 20.714 L 2.228 25.774 L 7.289 24.335 C 7.548 24.261 7.825 24.295 8.058 24.428 C 9.809 25.428 11.836 26 14 26 C 20.628 26 26 20.627 26 14 C 26 7.373 20.628 2 14 2 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 11.632 28 9.398 27.411 7.44 26.371 L 1.912 27.943 C 0.782 28.265 -0.262 27.22 0.059 26.09 L 1.631 20.563 C 0.59 18.605 0 16.37 0 14 Z M 8 11 C 8 10.448 8.448 10 9 10 L 19 10 C 19.552 10 20 10.448 20 11 C 20 11.552 19.552 12 19 12 L 9 12 C 8.448 12 8 11.552 8 11 Z M 9 16 C 8.448 16 8 16.448 8 17 C 8 17.552 8.448 18 9 18 L 15 18 C 15.552 18 16 17.552 16 17 C 16 16.448 15.552 16 15 16 L 9 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={28.002} viewBox="0 0 28 28.002" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28.002,
      }}>
        <path d={"M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 11.632 28 9.398 27.411 7.44 26.371 L 1.912 27.943 C 0.782 28.265 -0.262 27.22 0.059 26.09 L 1.631 20.563 C 0.59 18.605 0 16.37 0 14 Z M 8 11 C 8 11.552 8.448 12 9 12 L 19 12 C 19.552 12 20 11.552 20 11 C 20 10.448 19.552 10 19 10 L 9 10 C 8.448 10 8 10.448 8 11 Z M 9 16 C 8.448 16 8 16.448 8 17 C 8 17.552 8.448 18 9 18 L 15 18 C 15.552 18 16 17.552 16 17 C 16 16.448 15.552 16 15 16 L 9 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={24.002} viewBox="0 0 24 24.002" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 24,
        height: 24.002,
      }}>
        <path d={"M 7.069 21.273 C 8.538 22.056 10.216 22.5 12 22.5 C 17.799 22.5 22.5 17.799 22.5 12 C 22.5 6.201 17.799 1.5 12 1.5 C 6.201 1.5 1.5 6.201 1.5 12 C 1.5 13.785 1.945 15.464 2.729 16.934 C 2.912 17.276 2.954 17.676 2.848 18.05 L 1.615 22.387 L 5.953 21.153 C 6.326 21.047 6.726 21.09 7.069 21.273 Z M 12 24 C 9.964 24 8.045 23.492 6.363 22.596 L 1.593 23.953 C 0.651 24.221 -0.219 23.35 0.049 22.408 L 1.405 17.64 C 0.508 15.958 0 14.037 0 12 C 0 5.373 5.373 0 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 Z M 7.5 9.75 C 7.5 9.336 7.836 9 8.25 9 L 15.75 9 C 16.164 9 16.5 9.336 16.5 9.75 C 16.5 10.164 16.164 10.5 15.75 10.5 L 8.25 10.5 C 7.836 10.5 7.5 10.164 7.5 9.75 Z M 7.5 14.25 C 7.5 13.836 7.836 13.5 8.25 13.5 L 12.75 13.5 C 13.164 13.5 13.5 13.836 13.5 14.25 C 13.5 14.664 13.164 15 12.75 15 L 8.25 15 C 7.836 15 7.5 14.664 7.5 14.25 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={24.002} viewBox="0 0 24 24.002" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 24,
        height: 24.002,
      }}>
        <path d={"M 0 12 C 0 5.373 5.373 0 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 9.964 24 8.045 23.492 6.363 22.596 L 1.593 23.953 C 0.651 24.221 -0.219 23.35 0.049 22.408 L 1.405 17.64 C 0.508 15.958 0 14.037 0 12 Z M 7.5 9.75 C 7.5 10.164 7.836 10.5 8.25 10.5 L 15.75 10.5 C 16.164 10.5 16.5 10.164 16.5 9.75 C 16.5 9.336 16.164 9 15.75 9 L 8.25 9 C 7.836 9 7.5 9.336 7.5 9.75 Z M 8.25 13.5 C 7.836 13.5 7.5 13.836 7.5 14.25 C 7.5 14.664 7.836 15 8.25 15 L 12.75 15 C 13.164 15 13.5 14.664 13.5 14.25 C 13.5 13.836 13.164 13.5 12.75 13.5 L 8.25 13.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={20.002} viewBox="0 0 20 20.002" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 20,
        height: 20.002,
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 8.382 20 6.818 19.615 5.413 18.888 L 1.587 19.955 C 0.922 20.141 0.233 19.753 0.047 19.088 C -0.015 18.868 -0.015 18.635 0.047 18.415 L 1.115 14.592 C 0.386 13.186 0 11.62 0 10 C 0 4.477 4.477 0 10 0 Z M 10 1.5 C 5.306 1.5 1.5 5.306 1.5 10 C 1.5 11.47 1.873 12.883 2.573 14.137 L 2.724 14.407 L 1.611 18.391 L 5.598 17.279 L 5.867 17.43 C 7.12 18.128 8.532 18.5 10 18.5 C 14.694 18.5 18.5 14.694 18.5 10 C 18.5 5.306 14.694 1.5 10 1.5 Z M 6.75 11 L 11.248 11 C 11.662 11 11.998 11.336 11.998 11.75 C 11.998 12.13 11.716 12.443 11.35 12.493 L 11.248 12.5 L 6.75 12.5 C 6.336 12.5 6 12.164 6 11.75 C 6 11.37 6.282 11.057 6.648 11.007 L 6.75 11 L 11.248 11 L 6.75 11 Z M 6.75 7.5 L 13.254 7.5 C 13.669 7.5 14.004 7.836 14.004 8.25 C 14.004 8.63 13.722 8.943 13.356 8.993 L 13.254 9 L 6.75 9 C 6.336 9 6 8.664 6 8.25 C 6 7.87 6.282 7.557 6.648 7.507 L 6.75 7.5 L 13.254 7.5 L 6.75 7.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={20.003} viewBox="0 0 20 20.003" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 20,
        height: 20.003,
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 8.36 20 6.775 19.604 5.356 18.858 L 1.065 19.975 C 0.611 20.094 0.147 19.821 0.029 19.367 C -0.008 19.227 -0.008 19.079 0.029 18.939 L 1.145 14.65 C 0.397 13.229 0 11.643 0 10 C 0 4.477 4.477 0 10 0 Z M 11.252 11 L 6.75 11 L 6.648 11.007 C 6.282 11.057 6 11.37 6 11.75 C 6 12.13 6.282 12.443 6.648 12.493 L 6.75 12.5 L 11.252 12.5 L 11.353 12.493 C 11.72 12.443 12.002 12.13 12.002 11.75 C 12.002 11.37 11.72 11.057 11.353 11.007 L 11.252 11 Z M 13.25 7.5 L 6.75 7.5 L 6.648 7.507 C 6.282 7.557 6 7.87 6 8.25 C 6 8.63 6.282 8.943 6.648 8.993 L 6.75 9 L 13.25 9 L 13.352 8.993 C 13.718 8.943 14 8.63 14 8.25 C 14 7.87 13.718 7.557 13.352 7.507 L 13.25 7.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={16.000} viewBox="0 0 16 16.000" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 6.727 16 5.496 15.702 4.387 15.139 L 4.266 15.075 L 0.621 15.985 C 0.311 16.062 0.026 15.837 0.001 15.544 L 0.001 15.462 L 0.015 15.379 L 0.925 11.735 L 0.862 11.615 C 0.407 10.719 0.124 9.742 0.033 8.728 L 0.007 8.346 L 0 8 C 0 3.582 3.582 0 8 0 Z M 8 1 C 4.134 1 1 4.134 1 8 C 1 9.217 1.311 10.388 1.894 11.425 C 1.94 11.508 1.962 11.603 1.957 11.697 L 1.943 11.791 L 1.187 14.812 L 4.211 14.058 C 4.273 14.043 4.337 14.04 4.399 14.048 L 4.49 14.069 L 4.577 14.108 C 5.614 14.69 6.784 15 8 15 C 11.866 15 15 11.866 15 8 C 15 4.134 11.866 1 8 1 Z M 8.5 9 C 8.776 9 9 9.224 9 9.5 C 9 9.745 8.823 9.95 8.59 9.992 L 8.5 10 L 5.5 10 C 5.224 10 5 9.776 5 9.5 C 5 9.255 5.177 9.05 5.41 9.008 L 5.5 9 L 8.5 9 Z M 10.5 6 C 10.776 6 11 6.224 11 6.5 C 11 6.745 10.823 6.95 10.59 6.992 L 10.5 7 L 5.5 7 C 5.224 7 5 6.776 5 6.5 C 5 6.255 5.177 6.05 5.41 6.008 L 5.5 6 L 10.5 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={16.000} viewBox="0 0 16 16.000" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 6.727 16 5.496 15.702 4.387 15.139 L 4.266 15.075 L 0.621 15.985 C 0.311 16.062 0.026 15.837 0.001 15.544 L 0.001 15.462 L 0.015 15.379 L 0.925 11.735 L 0.862 11.615 C 0.407 10.719 0.124 9.742 0.033 8.728 L 0.007 8.346 L 0 8 C 0 3.582 3.582 0 8 0 Z M 8.5 9 L 5.5 9 L 5.41 9.008 C 5.177 9.05 5 9.255 5 9.5 C 5 9.745 5.177 9.95 5.41 9.992 L 5.5 10 L 8.5 10 L 8.59 9.992 C 8.823 9.95 9 9.745 9 9.5 C 9 9.255 8.823 9.05 8.59 9.008 L 8.5 9 Z M 10.5 6 L 5.5 6 L 5.41 6.008 C 5.177 6.05 5 6.255 5 6.5 C 5 6.745 5.177 6.95 5.41 6.992 L 5.5 7 L 10.5 7 L 10.59 6.992 C 10.823 6.95 11 6.745 11 6.5 C 11 6.255 10.823 6.05 10.59 6.008 L 10.5 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
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
        <path d={"M 4 5 C 4 4.724 4.224 4.5 4.5 4.5 L 7.5 4.5 C 7.776 4.5 8 4.724 8 5 C 8 5.276 7.776 5.5 7.5 5.5 L 4.5 5.5 C 4.224 5.5 4 5.276 4 5 Z M 4.5 6.5 C 4.224 6.5 4 6.724 4 7 C 4 7.276 4.224 7.5 4.5 7.5 L 6.5 7.5 C 6.776 7.5 7 7.276 7 7 C 7 6.724 6.776 6.5 6.5 6.5 L 4.5 6.5 Z M 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 C 12 9.314 9.314 12 6 12 C 4.906 12 3.88 11.707 2.996 11.195 L 0.658 11.974 C 0.482 12.033 0.288 11.989 0.154 11.861 C 0.02 11.733 -0.032 11.541 0.019 11.363 L 0.731 8.872 C 0.265 8.019 0 7.04 0 6 Z M 6 1 C 3.239 1 1 3.239 1 6 C 1 6.933 1.255 7.805 1.699 8.552 C 1.77 8.67 1.788 8.812 1.75 8.945 L 1.241 10.726 L 2.899 10.173 C 3.042 10.126 3.198 10.145 3.325 10.225 C 4.099 10.716 5.015 11 6 11 C 8.761 11 11 8.761 11 6 C 11 3.239 8.761 1 6 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
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
        <path d={"M 6 0 C 2.686 0 0 2.686 0 6 C 0 7.04 0.265 8.019 0.731 8.872 L 0.019 11.363 C -0.032 11.541 0.02 11.733 0.154 11.861 C 0.288 11.989 0.482 12.033 0.658 11.974 L 2.996 11.195 C 3.88 11.707 4.906 12 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 Z M 4 5 C 4 4.724 4.224 4.5 4.5 4.5 L 7.5 4.5 C 7.776 4.5 8 4.724 8 5 C 8 5.276 7.776 5.5 7.5 5.5 L 4.5 5.5 C 4.224 5.5 4 5.276 4 5 Z M 4.5 6.5 L 6.5 6.5 C 6.776 6.5 7 6.724 7 7 C 7 7.276 6.776 7.5 6.5 7.5 L 4.5 7.5 C 4.224 7.5 4 7.276 4 7 C 4 6.724 4.224 6.5 4.5 6.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 10,
        height: 10,
      }}>
        <path d={"M 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 C 10 7.761 7.761 10 5 10 C 4.127 10 3.306 9.776 2.591 9.382 L 0.646 9.974 C 0.469 10.028 0.277 9.98 0.146 9.849 C 0.016 9.719 -0.032 9.527 0.022 9.35 L 0.614 7.403 C 0.223 6.69 0 5.87 0 5 Z M 3 4.5 C 3 4.776 3.224 5 3.5 5 L 6.5 5 C 6.776 5 7 4.776 7 4.5 C 7 4.224 6.776 4 6.5 4 L 3.5 4 C 3.224 4 3 4.224 3 4.5 Z M 3.5 6 C 3.224 6 3 6.224 3 6.5 C 3 6.776 3.224 7 3.5 7 L 5.5 7 C 5.776 7 6 6.776 6 6.5 C 6 6.224 5.776 6 5.5 6 L 3.5 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 10,
        height: 10,
      }}>
        <path d={"M 3 4.5 C 3 4.224 3.224 4 3.5 4 L 6.5 4 C 6.776 4 7 4.224 7 4.5 C 7 4.776 6.776 5 6.5 5 L 3.5 5 C 3.224 5 3 4.776 3 4.5 Z M 3.5 6 C 3.224 6 3 6.224 3 6.5 C 3 6.776 3.224 7 3.5 7 L 5.5 7 C 5.776 7 6 6.776 6 6.5 C 6 6.224 5.776 6 5.5 6 L 3.5 6 Z M 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 C 10 7.761 7.761 10 5 10 C 4.127 10 3.306 9.776 2.591 9.382 L 0.646 9.974 C 0.469 10.028 0.277 9.98 0.146 9.849 C 0.016 9.719 -0.032 9.527 0.022 9.35 L 0.614 7.403 C 0.223 6.69 0 5.87 0 5 Z M 5 1 C 2.791 1 1 2.791 1 5 C 1 5.763 1.213 6.474 1.583 7.08 C 1.657 7.202 1.676 7.35 1.634 7.486 L 1.251 8.745 L 2.508 8.362 C 2.645 8.321 2.793 8.339 2.915 8.414 C 3.521 8.786 4.235 9 5 9 C 7.209 9 9 7.209 9 5 C 9 2.791 7.209 1 5 1 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={28} height={28.002} viewBox="0 0 28 28.002" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 1.999,
        width: 28,
        height: 28.002,
      }}>
        <path d={"M 14 0 C 6.268 0 0 6.268 0 14 C 0 16.37 0.59 18.605 1.631 20.563 L 0.059 26.09 C -0.262 27.22 0.782 28.264 1.912 27.943 L 7.44 26.371 C 9.398 27.411 11.631 28 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 Z M 14 27 C 11.877 27 9.771 26.478 7.91 25.488 L 7.555 25.299 L 1.639 26.982 C 1.59 26.996 1.542 27.003 1.495 27.003 C 1.344 27.003 1.197 26.927 1.1 26.8 C 1.033 26.711 0.964 26.563 1.021 26.364 L 2.703 20.449 L 2.514 20.094 C 1.524 18.232 1 16.124 1 14.001 C 1 6.833 6.832 1.001 14 1.001 C 21.168 1.001 27 6.833 27 14.001 C 27 21.169 21.168 27 14 27 Z M 19.5 11.001 L 8.5 11.001 C 8.224 11.001 8 10.777 8 10.501 C 8 10.225 8.224 10.001 8.5 10.001 L 19.5 10.001 C 19.776 10.001 20 10.225 20 10.501 C 20 10.777 19.776 11.001 19.5 11.001 Z M 15.5 17.001 L 8.5 17.001 C 8.224 17.001 8 16.777 8 16.501 C 8 16.225 8.224 16.001 8.5 16.001 L 15.5 16.001 C 15.776 16.001 16 16.225 16 16.501 C 16 16.777 15.776 17.001 15.5 17.001 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body0,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body1,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body2,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body3,
    // figma: Size=28, Theme=Regular
    "size=28|theme=regular": __body4,
    // figma: Size=28, Theme=Filled
    "size=28|theme=filled": __body5,
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body6,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body7,
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body8,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body9,
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body10,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body11,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body12,
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body13,
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body14,
  };
  return (__impls[__vkey(props)] ?? __body13)();
}
export default Chat;
