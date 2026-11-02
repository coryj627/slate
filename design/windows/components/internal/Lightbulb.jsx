import { __slateNorm } from './slateProps.jsx';
// figma node: 9322:593 Lightbulb (13 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Lightbulb(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24","28","32","48"],"theme":["regular","filled","light"]});
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
      <svg width={14.500} height={19.999} viewBox="0 0 14.500 19.999" fill="none" style={{
        position: "absolute",
        left: 4.75,
        top: 2.001,
        width: 14.5,
        height: 19.999,
      }}>
        <path d={"M 7.25 0 C 11.254 0 14.5 3.246 14.5 7.25 C 14.5 9.346 13.599 11.27 11.837 12.992 C 11.76 13.067 11.7 13.158 11.662 13.257 L 11.63 13.36 L 10.499 18.255 C 10.277 19.219 9.453 19.917 8.48 19.992 L 8.307 19.999 L 6.193 19.999 C 5.204 19.999 4.338 19.354 4.046 18.422 L 4.001 18.255 L 2.871 13.36 C 2.839 13.22 2.767 13.092 2.665 12.992 C 0.985 11.352 0.088 9.529 0.006 7.548 L 0 7.25 L 0.004 7.011 C 0.13 3.117 3.326 0 7.25 0 Z M 9.365 16.498 L 5.134 16.498 L 5.462 17.917 C 5.533 18.224 5.786 18.449 6.09 18.492 L 6.193 18.499 L 8.307 18.499 C 8.621 18.499 8.898 18.303 9.008 18.016 L 9.038 17.917 L 9.365 16.498 Z M 7.25 1.5 C 4.148 1.5 1.62 3.956 1.504 7.029 L 1.5 7.25 L 1.507 7.527 C 1.583 9.09 2.307 10.546 3.713 11.919 C 3.977 12.177 4.173 12.495 4.284 12.845 L 4.333 13.022 L 4.788 14.998 L 9.711 14.998 L 10.169 13.022 C 10.252 12.662 10.422 12.33 10.662 12.053 L 10.789 11.919 C 12.193 10.547 12.917 9.091 12.993 7.527 L 13 7.25 L 12.996 7.029 C 12.88 3.956 10.352 1.5 7.25 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14.500} height={19.999} viewBox="0 0 14.500 19.999" fill="none" style={{
        position: "absolute",
        left: 4.75,
        top: 2.001,
        width: 14.5,
        height: 19.999,
      }}>
        <path d={"M 10.788 16.998 L 10.497 18.257 C 10.274 19.22 9.451 19.917 8.479 19.992 L 8.305 19.999 L 6.194 19.999 C 5.204 19.999 4.339 19.354 4.047 18.422 L 4.001 18.255 L 3.711 16.998 L 10.788 16.998 Z M 7.25 0 C 11.254 0 14.5 3.246 14.5 7.25 C 14.5 9.387 13.564 11.343 11.735 13.091 C 11.699 13.124 11.675 13.168 11.664 13.215 L 11.136 15.498 L 3.364 15.498 L 2.838 13.215 C 2.827 13.168 2.802 13.124 2.767 13.091 C 0.936 11.343 0 9.387 0 7.25 C 0 3.246 3.246 0 7.25 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={16} viewBox="0 0 12 16" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 12,
        height: 16,
      }}>
        <path d={"M 6 0 C 9.314 0 12 2.597 12 5.8 C 12 7.477 11.255 9.016 9.796 10.394 C 9.732 10.454 9.683 10.527 9.651 10.607 L 9.625 10.688 L 8.689 14.605 C 8.505 15.376 7.824 15.935 7.018 15.995 L 6.875 16 L 5.125 16 C 4.307 16 3.59 15.484 3.349 14.739 L 3.311 14.605 L 2.376 10.689 C 2.35 10.577 2.29 10.474 2.205 10.394 C 0.815 9.082 0.073 7.624 0.005 6.039 L 0 5.8 L 0.003 5.609 C 0.108 2.494 2.753 0 6 0 Z M 8.045 13 L 3.955 13 L 4.274 14.334 L 4.3 14.43 C 4.397 14.73 4.676 14.952 5.012 14.993 L 5.125 15 L 6.838 15.001 L 6.944 14.997 C 7.286 14.972 7.573 14.761 7.685 14.472 L 7.716 14.373 L 8.045 13 Z M 6 1 C 3.368 1 1.212 2.953 1.015 5.414 L 1.003 5.626 L 1 5.779 L 1.004 5.996 C 1.06 7.3 1.678 8.521 2.892 9.667 C 3.08 9.845 3.222 10.065 3.306 10.308 L 3.349 10.456 L 3.716 12 L 8.285 12 L 8.672 10.385 L 8.723 10.234 C 8.809 10.02 8.941 9.826 9.109 9.667 C 10.382 8.465 11 7.181 11 5.8 C 11 3.157 8.769 1 6 1 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={16} viewBox="0 0 12 16" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 12,
        height: 16,
      }}>
        <path d={"M 9.073 13 L 8.689 14.605 C 8.505 15.376 7.824 15.935 7.018 15.995 L 6.875 16 L 5.125 16 C 4.307 16 3.59 15.484 3.349 14.739 L 3.311 14.605 L 2.928 13 L 9.073 13 Z M 6 0 C 9.314 0 12 2.597 12 5.8 C 12 7.477 11.255 9.016 9.796 10.394 C 9.732 10.454 9.683 10.527 9.651 10.607 L 9.625 10.688 L 9.311 12 L 2.689 12 L 2.376 10.689 C 2.35 10.577 2.29 10.474 2.205 10.394 C 0.815 9.082 0.073 7.624 0.005 6.039 L 0 5.8 L 0.003 5.609 C 0.108 2.494 2.753 0 6 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={9} height={12} viewBox="0 0 9 12" fill="none" style={{
        position: "absolute",
        left: 3.5,
        top: 2,
        width: 9,
        height: 12,
      }}>
        <path d={"M 1 4.5 C 1 2.567 2.567 1 4.5 1 C 6.433 1 8 2.567 8 4.5 C 8 5.477 7.6 6.36 6.954 6.995 C 6.761 7.185 6.591 7.432 6.51 7.731 L 6.164 9 L 2.836 9 L 2.49 7.731 C 2.409 7.432 2.239 7.185 2.046 6.995 C 1.4 6.36 1 5.477 1 4.5 Z M 3.109 10 L 5.891 10 L 5.719 10.632 C 5.659 10.849 5.462 11 5.236 11 L 3.764 11 C 3.538 11 3.341 10.849 3.281 10.632 L 3.109 10 Z M 4.5 0 C 2.015 0 0 2.015 0 4.5 C 0 5.756 0.515 6.893 1.345 7.708 C 1.441 7.804 1.5 7.902 1.526 7.995 L 2.317 10.895 C 2.495 11.547 3.087 12 3.764 12 L 5.236 12 C 5.913 12 6.505 11.547 6.683 10.895 L 7.474 7.995 C 7.5 7.902 7.559 7.804 7.655 7.708 C 8.485 6.893 9 5.756 9 4.5 C 9 2.015 6.985 0 4.5 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={9} height={12} viewBox="0 0 9 12" fill="none" style={{
        position: "absolute",
        left: 3.5,
        top: 2,
        width: 9,
        height: 12,
      }}>
        <path d={"M 4.5 0 C 2.015 0 0 2.015 0 4.5 C 0 5.756 0.515 6.893 1.345 7.708 C 1.441 7.804 1.5 7.902 1.526 7.995 L 1.8 9 L 7.2 9 L 7.474 7.995 C 7.5 7.902 7.559 7.804 7.655 7.708 C 8.485 6.893 9 5.756 9 4.5 C 9 2.015 6.985 0 4.5 0 Z M 6.927 10 L 2.073 10 L 2.317 10.895 C 2.495 11.547 3.087 12 3.764 12 L 5.236 12 C 5.913 12 6.505 11.547 6.683 10.895 L 6.927 10 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={18} height={24.000} viewBox="0 0 18 24.000" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 2,
        width: 18,
        height: 24,
      }}>
        <path d={"M 1.5 9 C 1.5 4.858 4.858 1.5 9 1.5 C 13.142 1.5 16.5 4.858 16.5 9 C 16.5 11.304 15.462 13.365 13.825 14.742 C 13.253 15.224 12.747 15.88 12.551 16.698 L 12.239 18 L 5.761 18 L 5.449 16.698 C 5.253 15.88 4.747 15.224 4.175 14.742 C 2.538 13.365 1.5 11.304 1.5 9 Z M 6.121 19.5 L 11.879 19.5 L 11.481 21.158 C 11.292 21.945 10.588 22.5 9.779 22.5 L 8.221 22.5 C 7.412 22.5 6.708 21.945 6.519 21.158 L 6.121 19.5 Z M 9 0 C 4.029 0 0 4.029 0 9 C 0 11.766 1.248 14.24 3.209 15.89 C 3.612 16.229 3.889 16.626 3.99 17.048 L 5.061 21.508 C 5.411 22.969 6.718 24 8.221 24 L 9.779 24 C 11.282 24 12.589 22.969 12.939 21.508 L 14.01 17.048 C 14.111 16.626 14.388 16.229 14.791 15.89 C 16.752 14.24 18 11.766 18 9 C 18 4.029 13.971 0 9 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={18} height={24.000} viewBox="0 0 18 24.000" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 2,
        width: 18,
        height: 24,
      }}>
        <path d={"M 9 0 C 4.029 0 0 4.029 0 9 C 0 11.766 1.248 14.24 3.209 15.89 C 3.612 16.229 3.889 16.626 3.99 17.048 L 4.339 18.5 L 13.661 18.5 L 14.01 17.048 C 14.111 16.626 14.388 16.229 14.791 15.89 C 16.752 14.24 18 11.766 18 9 C 18 4.029 13.971 0 9 0 Z M 13.301 20 L 4.699 20 L 5.061 21.508 C 5.411 22.969 6.718 24 8.221 24 L 9.779 24 C 11.282 24 12.589 22.969 12.939 21.508 L 13.301 20 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={28} viewBox="0 0 20 28" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 2,
        width: 20,
        height: 28,
      }}>
        <path d={"M 2 10 C 2 5.582 5.582 2 10 2 C 14.418 2 18 5.582 18 10 C 18 12.381 16.961 14.519 15.308 15.986 C 14.74 16.49 14.241 17.174 14.046 18.019 L 13.358 21 L 6.642 21 L 5.954 18.019 C 5.759 17.174 5.26 16.49 4.692 15.986 C 3.039 14.519 2 12.381 2 10 Z M 7.103 23 L 12.897 23 L 12.473 24.837 C 12.316 25.518 11.71 26 11.011 26 L 8.989 26 C 8.29 26 7.684 25.518 7.527 24.837 L 7.103 23 Z M 10 0 C 4.477 0 0 4.477 0 10 C 0 12.977 1.302 15.651 3.365 17.482 C 3.708 17.786 3.926 18.127 4.005 18.469 L 5.578 25.287 C 5.945 26.875 7.359 28 8.989 28 L 11.011 28 C 12.641 28 14.055 26.875 14.422 25.287 L 15.995 18.469 C 16.074 18.127 16.292 17.786 16.635 17.482 C 18.698 15.651 20 12.977 20 10 C 20 4.477 15.523 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={28} viewBox="0 0 20 28" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 2,
        width: 20,
        height: 28,
      }}>
        <path d={"M 10 0 C 4.477 0 0 4.477 0 10 C 0 12.977 1.302 15.651 3.365 17.482 C 3.708 17.786 3.926 18.127 4.005 18.469 L 4.82 22 L 15.18 22 L 15.995 18.469 C 16.074 18.127 16.292 17.786 16.635 17.482 C 18.698 15.651 20 12.977 20 10 C 20 4.477 15.523 0 10 0 Z M 14.719 24 L 5.281 24 L 5.578 25.287 C 5.945 26.875 7.359 28 8.989 28 L 11.011 28 C 12.641 28 14.055 26.875 14.422 25.287 L 14.719 24 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={29} height={40} viewBox="0 0 29 40" fill="none" style={{
        position: "absolute",
        left: 9.5,
        top: 4,
        width: 29,
        height: 40,
      }}>
        <path d={"M 2.5 14.5 C 2.5 7.873 7.873 2.5 14.5 2.5 C 21.127 2.5 26.5 7.873 26.5 14.5 C 26.5 18.004 24.999 21.156 22.602 23.352 C 21.887 24.007 21.268 24.871 20.998 25.924 L 19.825 30.5 L 9.175 30.5 L 8.002 25.924 C 7.732 24.871 7.113 24.007 6.398 23.352 C 4.001 21.156 2.5 18.004 2.5 14.5 Z M 9.816 33 L 19.184 33 L 18.464 35.809 C 18.209 36.804 17.312 37.5 16.284 37.5 L 12.716 37.5 C 11.688 37.5 10.791 36.804 10.536 35.809 L 9.816 33 Z M 14.5 0 C 6.492 0 0 6.492 0 14.5 C 0 18.734 1.817 22.546 4.71 25.196 C 5.158 25.607 5.458 26.069 5.58 26.545 L 8.115 36.43 C 8.653 38.531 10.547 40 12.716 40 L 16.284 40 C 18.453 40 20.347 38.531 20.885 36.43 L 23.42 26.545 C 23.542 26.069 23.842 25.607 24.29 25.196 C 27.183 22.546 29 18.734 29 14.5 C 29 6.492 22.508 0 14.5 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={29} height={40} viewBox="0 0 29 40" fill="none" style={{
        position: "absolute",
        left: 9.5,
        top: 4,
        width: 29,
        height: 40,
      }}>
        <path d={"M 14.5 0 C 6.492 0 0 6.492 0 14.5 C 0 18.734 1.817 22.546 4.71 25.196 C 5.158 25.607 5.458 26.069 5.58 26.545 L 6.851 31.5 L 22.149 31.5 L 23.42 26.545 C 23.542 26.069 23.842 25.607 24.29 25.196 C 27.183 22.546 29 18.734 29 14.5 C 29 6.492 22.508 0 14.5 0 Z M 21.508 34 L 7.492 34 L 8.115 36.43 C 8.653 38.531 10.547 40 12.716 40 L 16.284 40 C 18.453 40 20.347 38.531 20.885 36.43 L 21.508 34 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={28} viewBox="0 0 20 28" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 2,
        width: 20,
        height: 28,
      }}>
        <path d={"M 1 10 C 1 5.029 5.029 1 10 1 C 14.971 1 19 5.029 19 10 C 19 12.616 17.885 14.97 16.103 16.615 C 15.499 17.172 14.99 17.895 14.81 18.761 L 14.135 22 L 5.865 22 L 5.19 18.761 C 5.01 17.895 4.501 17.172 3.897 16.615 C 2.115 14.97 1 12.616 1 10 Z M 6.073 23 L 13.927 23 L 13.508 25.01 C 13.267 26.169 12.245 27 11.061 27 L 8.939 27 C 7.755 27 6.733 26.169 6.492 25.01 L 6.073 23 Z M 10 0 C 4.477 0 0 4.477 0 10 C 0 12.906 1.24 15.523 3.219 17.35 C 3.719 17.812 4.085 18.361 4.211 18.965 L 5.513 25.214 C 5.851 26.837 7.282 28 8.939 28 L 11.061 28 C 12.718 28 14.149 26.837 14.487 25.214 L 15.789 18.965 C 15.915 18.361 16.281 17.812 16.781 17.35 C 18.76 15.523 20 12.906 20 10 C 20 4.477 15.523 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=28, Theme=Regular
    "size=28|theme=regular": __body6,
    // figma: Size=28, Theme=Filled
    "size=28|theme=filled": __body7,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body8,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body9,
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body10,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body11,
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body12,
  };
  return (__impls[__vkey(props)] ?? __body4)();
}
export default Lightbulb;
