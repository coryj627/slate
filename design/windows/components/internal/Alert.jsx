import { __slateNorm } from './slateProps.jsx';
// figma node: 9328:24747 Alert (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Alert(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","28","32","48"],"theme":["regular","filled","light"]});
  const props = { ..._p, size: _p.size ?? "12", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20.007} height={22.002} viewBox="0 0 20.007 22.002" fill="none" style={{
        position: "absolute",
        left: 3.994,
        top: 3,
        width: 20.007,
        height: 22.002,
      }}>
        <path d={"M 10.006 0 C 14.615 0 18.368 3.669 18.503 8.246 L 18.507 8.501 L 18.507 13.113 L 19.921 16.757 C 19.959 16.855 19.985 16.958 19.997 17.062 L 20.007 17.22 C 20.007 17.883 19.504 18.428 18.858 18.493 L 18.727 18.5 L 13.507 18.501 C 13.507 20.435 11.939 22.002 10.006 22.002 C 8.136 22.002 6.609 20.538 6.509 18.693 L 6.504 18.499 L 1.28 18.5 C 1.122 18.5 0.964 18.47 0.816 18.413 C 0.199 18.173 -0.127 17.506 0.046 16.88 L 0.087 16.756 L 1.504 13.112 L 1.504 8.501 C 1.504 3.806 5.31 0 10.006 0 Z M 12.001 18.65 L 12.006 18.499 L 8.004 18.501 C 8.004 19.606 8.9 20.502 10.006 20.502 C 11.061 20.502 11.925 19.686 12.001 18.65 Z M 10.006 1.5 C 6.219 1.5 3.135 4.505 3.008 8.261 L 3.004 8.501 L 3.004 13.253 C 3.004 13.315 2.997 13.377 2.982 13.437 L 2.953 13.525 L 1.602 16.999 L 18.406 16.999 L 17.058 13.525 C 17.035 13.467 17.02 13.407 17.013 13.346 L 17.007 13.253 L 17.007 8.501 C 17.007 4.635 13.872 1.5 10.006 1.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20.007} height={22.002} viewBox="0 0 20.007 22.002" fill="none" style={{
        position: "absolute",
        left: 3.994,
        top: 3,
        width: 20.007,
        height: 22.002,
      }}>
        <path d={"M 13.471 19.002 C 13.228 20.699 11.769 22.002 10.006 22.002 C 8.242 22.002 6.783 20.699 6.54 19.002 L 13.471 19.002 Z M 10.006 0 C 14.615 0 18.368 3.669 18.503 8.246 L 18.503 8.501 L 18.507 8.501 L 18.507 12.613 L 19.921 16.257 C 19.959 16.356 19.985 16.458 19.997 16.563 L 20.007 16.72 C 20.007 17.383 19.504 17.928 18.858 17.994 L 18.727 18 L 1.28 18 C 1.122 18 0.964 17.971 0.816 17.913 C 0.199 17.673 -0.127 17.006 0.046 16.381 L 0.087 16.256 L 1.504 12.612 L 1.504 8.501 C 1.504 3.806 5.31 0 10.006 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={17.960} height={19.505} viewBox="0 0 17.960 19.505" fill="none" style={{
        position: "absolute",
        left: 3.025,
        top: 1.996,
        width: 17.96,
        height: 19.505,
      }}>
        <path d={"M 8.975 0 C 13.025 0 16.332 3.195 16.471 7.249 L 16.475 7.5 L 16.475 11.597 L 17.855 14.753 C 17.925 14.911 17.96 15.081 17.96 15.254 C 17.96 15.944 17.401 16.504 16.71 16.504 L 11.975 16.505 C 11.975 18.162 10.632 19.505 8.975 19.505 C 7.378 19.505 6.072 18.256 5.981 16.682 L 5.975 16.503 L 1.25 16.504 C 1.079 16.504 0.909 16.469 0.752 16.4 C 0.119 16.125 -0.171 15.389 0.104 14.756 L 1.475 11.598 L 1.475 7.5 C 1.476 3.345 4.828 0 8.975 0 Z M 10.475 16.503 L 7.475 16.505 C 7.475 17.334 8.147 18.005 8.975 18.005 C 9.755 18.005 10.396 17.41 10.469 16.65 L 10.475 16.503 Z M 8.975 1.5 C 5.655 1.5 2.976 4.174 2.975 7.5 L 2.975 11.91 L 1.631 15.004 L 16.328 15.004 L 14.975 11.911 L 14.976 7.513 L 14.972 7.288 C 14.861 4.054 12.217 1.5 8.975 1.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={18.000} height={19.498} viewBox="0 0 18.000 19.498" fill="none" style={{
        position: "absolute",
        left: 3.002,
        top: 2.004,
        width: 18,
        height: 19.498,
      }}>
        <path d={"M 6.04 16.999 L 11.956 16.999 C 11.718 18.417 10.484 19.498 8.998 19.498 C 7.512 19.498 6.278 18.417 6.04 16.999 Z M 8.998 0 C 13.14 0 16.498 3.358 16.498 7.5 L 16.498 11.499 L 17.916 14.659 C 17.971 14.781 18 14.914 18 15.048 C 18 15.572 17.574 15.998 17.05 15.998 L 0.95 15.998 C 0.816 15.998 0.684 15.969 0.562 15.915 C 0.083 15.7 -0.131 15.138 0.083 14.659 L 1.498 11.499 L 1.498 7.487 L 1.503 7.237 C 1.642 3.206 4.954 0 8.998 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={13.996} height={15.500} viewBox="0 0 13.996 15.500" fill="none" style={{
        position: "absolute",
        left: 3.006,
        top: 2,
        width: 13.996,
        height: 15.5,
      }}>
        <path d={"M 6.992 0 C 10.141 0 12.736 2.335 12.977 5.355 L 12.99 5.578 L 12.994 5.802 L 12.993 9.398 L 13.919 11.62 C 13.941 11.674 13.959 11.73 13.972 11.787 L 13.987 11.873 L 13.996 12.005 C 13.996 12.453 13.699 12.839 13.247 12.968 L 13.13 12.995 L 12.996 13.005 L 9.494 13.004 L 9.489 13.165 C 9.404 14.469 8.32 15.5 6.994 15.5 C 5.669 15.5 4.584 14.468 4.5 13.164 L 4.494 13.004 L 0.993 13.005 C 0.905 13.005 0.818 12.993 0.733 12.97 L 0.609 12.928 C 0.198 12.757 -0.044 12.339 0.007 11.876 L 0.028 11.748 L 0.07 11.62 L 0.993 9.401 L 0.994 5.793 L 0.999 5.568 C 1.121 2.451 3.765 0 6.992 0 Z M 8.494 13.004 L 5.494 13.004 L 5.501 13.145 C 5.57 13.858 6.137 14.425 6.85 14.493 L 6.994 14.5 C 7.774 14.5 8.415 13.905 8.488 13.144 L 8.494 13.004 Z M 6.992 1 C 4.369 1 2.222 2.924 2.012 5.385 L 1.998 5.597 L 1.994 5.802 L 1.994 9.5 L 1.956 9.692 L 0.993 12.005 L 12.951 12.007 L 12.996 12.005 L 12.033 9.692 L 11.994 9.5 L 11.995 5.812 L 11.99 5.608 C 11.885 3.035 9.689 1 6.992 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={13.989} height={16} viewBox="0 0 13.989 16" fill="none" style={{
        position: "absolute",
        left: 3.01,
        top: 2,
        width: 13.989,
        height: 16,
      }}>
        <path d={"M 9.44 14.002 C 9.208 15.142 8.199 16 6.99 16 C 5.781 16 4.773 15.142 4.541 14.002 L 9.44 14.002 Z M 6.988 0 C 10.137 0 12.732 2.335 12.973 5.355 L 12.986 5.578 L 12.99 5.802 L 12.989 9.408 L 13.943 11.995 L 13.969 12.08 L 13.984 12.166 L 13.989 12.255 C 13.989 12.57 13.794 12.845 13.468 12.962 L 13.354 12.995 L 13.239 13.005 L 0.742 13.005 C 0.653 13.005 0.565 12.989 0.482 12.958 C 0.196 12.853 0.007 12.586 0 12.242 L 0.005 12.125 L 0.038 11.995 L 0.989 9.411 L 0.991 5.793 L 0.995 5.568 C 1.117 2.451 3.761 0 6.988 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={11} height={12} viewBox="0 0 11 12" fill="none" style={{
        position: "absolute",
        left: 2.5,
        top: 2,
        width: 11,
        height: 12,
      }}>
        <path d={"M 5.5 0 C 3.015 0 1 2.015 1 4.5 L 1 6.901 L 0.036 9.314 C -0.026 9.468 -0.007 9.643 0.086 9.78 C 0.179 9.918 0.334 10 0.5 10 L 3.5 10 C 3.5 11.108 4.392 12 5.5 12 C 6.608 12 7.5 11.108 7.5 10 L 10.5 10 C 10.666 10 10.821 9.918 10.914 9.78 C 11.007 9.643 11.026 9.468 10.964 9.314 L 10 6.901 L 10 4.5 C 10 2.015 7.985 0 5.5 0 Z M 6.5 10 C 6.5 10.556 6.056 11 5.5 11 C 4.944 11 4.5 10.556 4.5 10 L 6.5 10 Z M 2 4.5 C 2 2.567 3.567 1 5.5 1 C 7.433 1 9 2.567 9 4.5 L 9 6.998 C 9 7.061 9.012 7.124 9.036 7.183 L 9.762 9 L 1.238 9 L 1.964 7.183 C 1.988 7.124 2 7.061 2 6.998 L 2 4.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={11} height={12.500} viewBox="0 0 11 12.500" fill="none" style={{
        position: "absolute",
        left: 2.5,
        top: 2,
        width: 11,
        height: 12.5,
      }}>
        <path d={"M 5.5 0 C 3.015 0 1 2.015 1 4.5 L 1 6.901 L 0.036 9.314 C -0.026 9.468 -0.007 9.643 0.086 9.78 C 0.179 9.918 0.334 10 0.5 10 L 10.5 10 C 10.666 10 10.821 9.918 10.914 9.78 C 11.007 9.643 11.026 9.468 10.964 9.314 L 10 6.901 L 10 4.5 C 10 2.015 7.985 0 5.5 0 Z M 5.5 12.5 C 4.568 12.5 3.785 11.863 3.563 11 L 7.437 11 C 7.215 11.863 6.432 12.5 5.5 12.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 4,
        width: 22,
        height: 24,
      }}>
        <path d={"M 11 0 C 6.029 0 2 4.029 2 9 L 2 13.803 L 0.071 18.629 C -0.052 18.937 -0.014 19.286 0.172 19.561 C 0.358 19.835 0.668 20 1 20 L 7 20 C 7 22.217 8.783 24 11 24 C 13.217 24 15 22.217 15 20 L 21 20 C 21.332 20 21.642 19.835 21.828 19.561 C 22.014 19.286 22.052 18.937 21.929 18.629 L 20 13.803 L 20 9 C 20 4.029 15.971 0 11 0 Z M 13 20 C 13 21.112 12.112 22 11 22 C 9.888 22 9 21.112 9 20 L 13 20 Z M 4 9 C 4 5.134 7.134 2 11 2 C 14.866 2 18 5.134 18 9 L 18 13.995 C 18 14.122 18.024 14.248 18.071 14.366 L 19.523 18 L 2.477 18 L 3.929 14.366 C 3.976 14.248 4 14.122 4 13.995 L 4 9 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={22} height={24} viewBox="0 0 22 24" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 4,
        width: 22,
        height: 24,
      }}>
        <path d={"M 2 9 C 2 4.029 6.029 0 11 0 C 15.971 0 20 4.029 20 9 L 20 12.807 L 21.928 17.629 C 22.052 17.937 22.014 18.286 21.828 18.561 C 21.642 18.835 21.332 19 21 19 L 1 19 C 0.668 19 0.358 18.835 0.172 18.561 C -0.014 18.286 -0.052 17.937 0.072 17.629 L 2 12.807 L 2 9 Z M 7.125 21 C 7.567 22.729 9.129 24 11 24 C 12.871 24 14.433 22.729 14.875 21 L 7.125 21 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={35.996} height={40} viewBox="0 0 35.996 40" fill="none" style={{
        position: "absolute",
        left: 6.002,
        top: 4,
        width: 35.996,
        height: 40,
      }}>
        <path d={"M 5.498 15 L 5.498 23.75 C 5.498 23.924 5.462 24.096 5.392 24.255 L 2.636 30.5 L 33.36 30.5 L 30.605 24.255 C 30.534 24.096 30.498 23.924 30.498 23.75 L 30.498 15 C 30.498 8.096 24.902 2.5 17.998 2.5 C 11.095 2.5 5.498 8.096 5.498 15 Z M 11.998 33 L 2.253 33 C 0.627 33 -0.462 31.329 0.194 29.842 L 2.998 23.486 L 2.998 15 C 2.998 6.716 9.714 0 17.998 0 C 26.282 0 32.998 6.716 32.998 15 L 32.998 23.486 L 35.802 29.842 C 36.459 31.329 35.37 33 33.744 33 L 23.998 33 L 23.998 34 C 23.998 37.314 21.312 40 17.998 40 C 14.685 40 11.998 37.314 11.998 34 L 11.998 33 Z M 21.498 33 L 14.498 33 L 14.498 34 C 14.498 35.933 16.065 37.5 17.998 37.5 C 19.931 37.5 21.498 35.933 21.498 34 L 21.498 33 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={35.996} height={40} viewBox="0 0 35.996 40" fill="none" style={{
        position: "absolute",
        left: 6.002,
        top: 4,
        width: 35.996,
        height: 40,
      }}>
        <path d={"M 17.998 0 C 9.714 0 2.998 6.716 2.998 15 L 2.998 23.486 L 0.194 29.842 C -0.462 31.329 0.627 33 2.253 33 L 33.744 33 C 35.37 33 36.459 31.329 35.802 29.842 L 32.998 23.486 L 32.998 15 C 32.998 6.716 26.282 0 17.998 0 Z M 17.998 40 C 15.025 40 12.557 37.838 12.081 35 L 23.915 35 C 23.439 37.838 20.971 40 17.998 40 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 2 4 L 2 5.5 C 2 5.578 1.982 5.654 1.947 5.724 L 1.309 7 L 8.691 7 L 8.053 5.724 C 8.018 5.654 8 5.578 8 5.5 L 8 4 C 8 2.343 6.657 1 5 1 C 3.343 1 2 2.343 2 4 Z M 3 8 L 0.5 8 C 0.327 8 0.166 7.91 0.075 7.763 C -0.016 7.615 -0.025 7.431 0.053 7.276 L 1 5.382 L 1 4 C 1 1.791 2.791 0 5 0 C 7.209 0 9 1.791 9 4 L 9 5.382 L 9.947 7.276 C 10.025 7.431 10.016 7.615 9.925 7.763 C 9.834 7.91 9.673 8 9.5 8 L 7 8 C 7 9.105 6.105 10 5 10 C 3.895 10 3 9.105 3 8 Z M 6 8 L 4 8 C 4 8.552 4.448 9 5 9 C 5.552 9 6 8.552 6 8 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={10.500} viewBox="0 0 10 10.500" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 10,
        height: 10.5,
      }}>
        <path d={"M 5 0 C 2.791 0 1 1.791 1 4 L 1 5.382 L 0.053 7.276 C -0.025 7.431 -0.016 7.615 0.075 7.763 C 0.166 7.91 0.327 8 0.5 8 L 9.5 8 C 9.673 8 9.834 7.91 9.925 7.763 C 10.016 7.615 10.025 7.431 9.947 7.276 L 9 5.382 L 9 4 C 9 1.791 7.209 0 5 0 Z M 5 10.5 C 4.068 10.5 3.285 9.863 3.063 9 L 6.937 9 C 6.715 9.863 5.932 10.5 5 10.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={22.005} height={24} viewBox="0 0 22.005 24" fill="none" style={{
        position: "absolute",
        left: 4.997,
        top: 4,
        width: 22.005,
        height: 24,
      }}>
        <path d={"M 11.002 0 C 6.032 0 2.002 4.029 2.002 9 L 2.002 13.804 L 0.073 18.629 C -0.19 19.285 0.294 20 1.001 20 L 7.002 20 C 7.002 22.209 8.793 24 11.002 24 C 13.212 24 15.002 22.209 15.002 20 L 21.004 20 C 21.711 20 22.195 19.285 21.932 18.629 L 20.002 13.804 L 20.002 9 C 20.002 4.029 15.973 0 11.002 0 Z M 14.002 20 C 14.002 21.657 12.659 23 11.002 23 C 9.346 23 8.002 21.657 8.002 20 L 14.002 20 Z M 3.002 9 C 3.002 4.582 6.584 1 11.002 1 C 15.421 1 19.002 4.582 19.002 9 L 19.002 13.804 C 19.002 13.931 19.027 14.057 19.074 14.175 L 21.004 19 L 1.001 19 L 2.931 14.175 C 2.978 14.057 3.002 13.931 3.002 13.804 L 3.002 9 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=28, Theme=Regular
    "size=28|theme=regular": __body0,
    // figma: Size=28, Theme=Filled
    "size=28|theme=filled": __body1,
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body2,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body3,
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body4,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body5,
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body6,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body7,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body8,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body9,
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body10,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body11,
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body12,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body13,
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body14,
  };
  return (__impls[__vkey(props)] ?? __body12)();
}
export default Alert;
