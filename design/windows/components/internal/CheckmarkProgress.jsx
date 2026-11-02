import { __slateNorm } from './slateProps.jsx';
// figma node: 9738:20 Checkmark Progress (14 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function CheckmarkProgress(_p = {}) {
  _p = __slateNorm(_p, {"size":["512","256","192","384","128","64","52"],"theme":["light","dark"]});
  const props = { ..._p, size: _p.size ?? "512", theme: _p.theme ?? "light" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: 10,
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 512,
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: 10,
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 384,
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: 10,
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 256,
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: 10,
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 192,
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 128,
      height: 128,
      overflow: "hidden",
      display: "flex",
      flexDirection: "row",
      gap: 10,
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 128,
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <svg width={56.009} height={56.009} viewBox="0 0 56.009 56.009" fill="none" style={{
        position: "absolute",
        left: 3.996,
        top: 3.996,
        width: 56.009,
        height: 56.009,
      }}>
        <path d={"M 42.727 19.505 C 47.421 27.636 44.635 38.033 36.505 42.727 C 28.374 47.421 17.977 44.636 13.282 36.505 C 11.734 33.822 10.999 30.894 11.002 28.004 L 10.982 28.004 C 10.997 27.84 11.004 27.673 11.004 27.504 C 11.004 24.467 8.542 22.004 5.504 22.004 C 2.467 22.004 0.004 24.467 0.004 27.504 C 0.004 27.673 0.012 27.84 0.027 28.004 L 0 28.004 C -0.004 32.763 1.205 37.587 3.756 42.005 C 3.814 42.105 3.872 42.205 3.931 42.304 C 3.955 42.349 3.98 42.393 4.006 42.438 C 4.073 42.553 4.143 42.665 4.217 42.774 C 11.994 55.317 28.214 59.663 41.221 52.69 C 41.339 52.632 41.456 52.57 41.572 52.503 C 41.616 52.478 41.66 52.452 41.704 52.425 C 41.804 52.368 41.905 52.311 42.005 52.253 C 55.397 44.521 59.985 27.397 52.253 14.005 C 52.195 13.904 52.137 13.805 52.078 13.706 C 52.054 13.661 52.029 13.616 52.003 13.572 C 51.936 13.456 51.866 13.344 51.793 13.235 C 43.858 0.439 27.137 -3.826 14.005 3.756 L 14.016 3.775 C 13.866 3.845 13.717 3.922 13.571 4.006 C 10.941 5.525 10.04 8.888 11.558 11.519 C 13.077 14.15 16.441 15.051 19.071 13.532 C 19.217 13.448 19.358 13.358 19.493 13.263 L 19.504 13.282 C 27.635 8.588 38.033 11.374 42.727 19.505 Z"} fill="currentColor" fillRule="evenodd" />
      </svg>
      <svg width={23} height={18} viewBox="0 0 23 18" fill="none" style={{
        position: "absolute",
        left: 33,
        top: 10,
        width: 23,
        height: 18,
        color: "rgb(239,226,105)",
      }}>
        <path d={"M 18.75 8.375 C 18.75 8.582 18.582 8.75 18.375 8.75 C 18.168 8.75 18 8.582 18 8.375 C 18 8.168 18.168 8 18.375 8 C 18.582 8 18.75 8.168 18.75 8.375 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 1 0.5 C 1 0.776 0.776 1 0.5 1 C 0.224 1 0 0.776 0 0.5 C 0 0.224 0.224 0 0.5 0 C 0.776 0 1 0.224 1 0.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 8 3.5 C 8 3.776 7.776 4 7.5 4 C 7.224 4 7 3.776 7 3.5 C 7 3.224 7.224 3 7.5 3 C 7.776 3 8 3.224 8 3.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 11 3.5 C 11 3.776 10.776 4 10.5 4 C 10.224 4 10 3.776 10 3.5 C 10 3.224 10.224 3 10.5 3 C 10.776 3 11 3.224 11 3.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 23 17.5 C 23 17.776 22.776 18 22.5 18 C 22.224 18 22 17.776 22 17.5 C 22 17.224 22.224 17 22.5 17 C 22.776 17 23 17.224 23 17.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 14.8,
        top: 7.3,
        width: 11,
        height: 11,
        borderRadius: "50%",
        background: "radial-gradient(7.871px 3.983px at 48.28% 100.00%, rgba(255,179,187,0.5) 0.00%, rgba(254,98,93,0) 100.00%), radial-gradient(9.763px 9.763px at 50.00% 50.00%, rgba(255,255,255,0) 44.60%, rgba(255,255,255,0.8) 84.14%), radial-gradient(3.880px 3.880px at 59.46% 26.43%, rgb(237,218,253) 0.00%, rgba(216,189,239,0.5784) 57.33%, rgba(188,149,220,0) 100.00%), radial-gradient(8.134px 8.134px at 58.14% 28.41%, rgb(218,176,242) 0.00%, rgba(209,165,234,0) 80.61%), radial-gradient(10.394px 10.394px at 57.47% 35.67%, rgb(197,143,241) 10.32%, rgb(157,80,238) 32.29%, rgb(123,48,234) 51.04%, rgb(203,82,133) 78.13%)",
      }} />
      <svg width={22.105} height={20} viewBox="0 0 22.105 20" fill="none" style={{
        position: "absolute",
        left: 20,
        top: 22,
        width: 22.105,
        height: 20,
      }}>
        <path d={"M 11.564 18.875 C 11.507 18.942 11.445 19.009 11.38 19.072 C 10.121 20.309 8.08 20.309 6.822 19.072 L 0.944 13.294 C -0.315 12.058 -0.315 10.053 0.944 8.816 C 2.202 7.579 4.24 7.579 5.499 8.816 L 8.141 11.413 C 8.448 11.714 8.957 11.669 9.205 11.319 L 16.236 1.361 C 16.72 0.676 17.429 0.237 18.196 0.073 C 19.038 -0.107 19.954 0.043 20.72 0.565 C 22.183 1.562 22.544 3.534 21.53 4.971 L 12.168 18.232 C 11.994 18.479 11.79 18.694 11.564 18.875 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 52,
      height: 52,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <svg width={45.507} height={45.508} viewBox="0 0 45.507 45.508" fill="none" style={{
        position: "absolute",
        left: 3.246,
        top: 3.246,
        width: 45.507,
        height: 45.508,
      }}>
        <path d={"M 34.716 15.848 C 38.53 22.454 36.266 30.902 29.66 34.716 C 23.054 38.53 14.606 36.266 10.792 29.66 C 9.534 27.481 8.937 25.101 8.939 22.754 L 8.923 22.754 C 8.935 22.62 8.941 22.484 8.941 22.347 C 8.941 19.879 6.94 17.879 4.472 17.879 C 2.004 17.879 0.004 19.879 0.004 22.347 C 0.004 22.484 0.01 22.62 0.022 22.754 L 0 22.754 C -0.004 26.62 0.979 30.539 3.052 34.129 C 3.099 34.21 3.146 34.291 3.194 34.372 C 3.214 34.408 3.234 34.445 3.255 34.481 C 3.309 34.574 3.366 34.666 3.426 34.754 C 9.745 44.945 22.924 48.476 33.492 42.81 C 33.588 42.764 33.683 42.713 33.777 42.659 C 33.813 42.638 33.849 42.617 33.884 42.595 C 33.966 42.549 34.047 42.503 34.129 42.456 C 45.01 36.174 48.738 22.26 42.456 11.379 C 42.409 11.297 42.361 11.216 42.313 11.136 C 42.294 11.099 42.273 11.063 42.253 11.027 C 42.198 10.933 42.141 10.842 42.081 10.753 C 35.635 0.357 22.049 -3.109 11.379 3.052 L 11.388 3.067 C 11.266 3.124 11.145 3.186 11.027 3.255 C 8.889 4.489 8.157 7.222 9.391 9.359 C 10.625 11.497 13.358 12.229 15.496 10.995 C 15.614 10.926 15.728 10.853 15.838 10.776 L 15.847 10.792 C 22.454 6.978 30.901 9.241 34.716 15.848 Z"} fill="currentColor" fillRule="evenodd" />
      </svg>
      <svg width={18.688} height={14.625} viewBox="0 0 18.688 14.625" fill="none" style={{
        position: "absolute",
        left: 26.813,
        top: 8.125,
        width: 18.688,
        height: 14.625,
        color: "rgb(239,226,105)",
      }}>
        <path d={"M 15.234 6.805 C 15.234 6.973 15.098 7.109 14.93 7.109 C 14.761 7.109 14.625 6.973 14.625 6.805 C 14.625 6.636 14.761 6.5 14.93 6.5 C 15.098 6.5 15.234 6.636 15.234 6.805 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 0.813 0.406 C 0.813 0.631 0.631 0.813 0.406 0.813 C 0.182 0.813 0 0.631 0 0.406 C 0 0.182 0.182 0 0.406 0 C 0.631 0 0.813 0.182 0.813 0.406 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 6.5 2.844 C 6.5 3.068 6.318 3.25 6.094 3.25 C 5.869 3.25 5.688 3.068 5.688 2.844 C 5.688 2.619 5.869 2.438 6.094 2.438 C 6.318 2.438 6.5 2.619 6.5 2.844 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 8.938 2.844 C 8.938 3.068 8.756 3.25 8.531 3.25 C 8.307 3.25 8.125 3.068 8.125 2.844 C 8.125 2.619 8.307 2.438 8.531 2.438 C 8.756 2.438 8.938 2.619 8.938 2.844 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 18.688 14.219 C 18.688 14.443 18.506 14.625 18.281 14.625 C 18.057 14.625 17.875 14.443 17.875 14.219 C 17.875 13.994 18.057 13.813 18.281 13.813 C 18.506 13.813 18.688 13.994 18.688 14.219 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 12.025,
        top: 5.931,
        width: 8.938,
        height: 8.938,
        borderRadius: "50%",
        background: "radial-gradient(6.395px 3.236px at 48.28% 100.00%, rgba(255,179,187,0.5) 0.00%, rgba(254,98,93,0) 100.00%), radial-gradient(7.933px 7.933px at 50.00% 50.00%, rgba(255,255,255,0) 44.60%, rgba(255,255,255,0.8) 84.14%), radial-gradient(3.152px 3.152px at 59.46% 26.43%, rgb(237,218,253) 0.00%, rgba(216,189,239,0.5784) 57.33%, rgba(188,149,220,0) 100.00%), radial-gradient(6.609px 6.609px at 58.14% 28.41%, rgb(218,176,242) 0.00%, rgba(209,165,234,0) 80.61%), radial-gradient(8.445px 8.445px at 57.47% 35.67%, rgb(197,143,241) 10.32%, rgb(157,80,238) 32.29%, rgb(123,48,234) 51.04%, rgb(203,82,133) 78.13%)",
      }} />
      <svg width={17.961} height={16.250} viewBox="0 0 17.961 16.250" fill="none" style={{
        position: "absolute",
        left: 16.25,
        top: 17.875,
        width: 17.961,
        height: 16.25,
      }}>
        <path d={"M 9.396 15.336 C 9.35 15.39 9.299 15.445 9.246 15.496 C 8.224 16.501 6.565 16.501 5.543 15.496 L 0.767 10.801 C -0.256 9.797 -0.256 8.168 0.767 7.163 C 1.789 6.158 3.445 6.158 4.468 7.163 L 6.615 9.273 C 6.864 9.518 7.278 9.481 7.479 9.197 L 13.192 1.106 C 13.585 0.55 14.161 0.193 14.784 0.059 C 15.468 -0.087 16.213 0.035 16.835 0.459 C 18.023 1.269 18.317 2.871 17.493 4.039 L 9.886 14.813 C 9.745 15.014 9.579 15.188 9.396 15.336 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <svg width={56.009} height={56.009} viewBox="0 0 56.009 56.009" fill="none" style={{
        position: "absolute",
        left: 3.996,
        top: 3.996,
        width: 56.009,
        height: 56.009,
      }}>
        <path d={"M 42.727 19.505 C 47.421 27.636 44.635 38.033 36.505 42.727 C 28.374 47.421 17.977 44.636 13.282 36.505 C 11.734 33.822 10.999 30.894 11.002 28.004 L 10.982 28.004 C 10.997 27.84 11.004 27.673 11.004 27.504 C 11.004 24.467 8.542 22.004 5.504 22.004 C 2.467 22.004 0.004 24.467 0.004 27.504 C 0.004 27.673 0.012 27.84 0.027 28.004 L 0 28.004 C -0.004 32.763 1.205 37.587 3.756 42.005 C 3.814 42.105 3.872 42.205 3.931 42.304 C 3.955 42.349 3.98 42.393 4.006 42.438 C 4.073 42.553 4.143 42.665 4.217 42.774 C 11.994 55.317 28.214 59.663 41.221 52.69 C 41.339 52.632 41.456 52.57 41.572 52.503 C 41.616 52.478 41.66 52.452 41.704 52.425 C 41.804 52.368 41.905 52.311 42.005 52.253 C 55.397 44.521 59.985 27.397 52.253 14.005 C 52.195 13.904 52.137 13.805 52.078 13.706 C 52.054 13.661 52.029 13.616 52.003 13.572 C 51.936 13.456 51.866 13.344 51.793 13.235 C 43.858 0.439 27.137 -3.826 14.005 3.756 L 14.016 3.775 C 13.866 3.845 13.717 3.922 13.571 4.006 C 10.941 5.525 10.04 8.888 11.558 11.519 C 13.077 14.15 16.441 15.051 19.071 13.532 C 19.217 13.448 19.358 13.358 19.493 13.263 L 19.504 13.282 C 27.635 8.588 38.033 11.374 42.727 19.505 Z"} fill="currentColor" fillRule="evenodd" />
      </svg>
      <svg width={23.750} height={38.750} viewBox="0 0 23.750 38.750" fill="none" style={{
        position: "absolute",
        left: 33,
        top: 10,
        width: 23.75,
        height: 38.75,
        color: "rgb(239,226,105)",
      }}>
        <path d={"M 18.75 8.375 C 18.75 8.582 18.582 8.75 18.375 8.75 C 18.168 8.75 18 8.582 18 8.375 C 18 8.168 18.168 8 18.375 8 C 18.582 8 18.75 8.168 18.75 8.375 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 1 0.5 C 1 0.776 0.776 1 0.5 1 C 0.224 1 0 0.776 0 0.5 C 0 0.224 0.224 0 0.5 0 C 0.776 0 1 0.224 1 0.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 8 3.5 C 8 3.776 7.776 4 7.5 4 C 7.224 4 7 3.776 7 3.5 C 7 3.224 7.224 3 7.5 3 C 7.776 3 8 3.224 8 3.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 11 3.5 C 11 3.776 10.776 4 10.5 4 C 10.224 4 10 3.776 10 3.5 C 10 3.224 10.224 3 10.5 3 C 10.776 3 11 3.224 11 3.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 23 17.5 C 23 17.776 22.776 18 22.5 18 C 22.224 18 22 17.776 22 17.5 C 22 17.224 22.224 17 22.5 17 C 22.776 17 23 17.224 23 17.5 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 12.75 38.375 C 12.75 38.582 12.582 38.75 12.375 38.75 C 12.168 38.75 12 38.582 12 38.375 C 12 38.168 12.168 38 12.375 38 C 12.582 38 12.75 38.168 12.75 38.375 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 15.75 32.375 C 15.75 32.582 15.582 32.75 15.375 32.75 C 15.168 32.75 15 32.582 15 32.375 C 15 32.168 15.168 32 15.375 32 C 15.582 32 15.75 32.168 15.75 32.375 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 23.75 31.5 C 23.75 31.707 23.582 31.875 23.375 31.875 C 23.168 31.875 23 31.707 23 31.5 C 23 31.293 23.168 31.125 23.375 31.125 C 23.582 31.125 23.75 31.293 23.75 31.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 14.8,
        top: 7.3,
        width: 11,
        height: 11,
        borderRadius: "50%",
        background: "radial-gradient(7.871px 3.983px at 48.28% 100.00%, rgba(255,179,187,0.5) 0.00%, rgba(254,98,93,0) 100.00%), radial-gradient(9.763px 9.763px at 50.00% 50.00%, rgba(255,255,255,0) 44.60%, rgba(255,255,255,0.8) 84.14%), radial-gradient(3.880px 3.880px at 59.46% 26.43%, rgb(237,218,253) 0.00%, rgba(216,189,239,0.5784) 57.33%, rgba(188,149,220,0) 100.00%), radial-gradient(8.134px 8.134px at 58.14% 28.41%, rgb(218,176,242) 0.00%, rgba(209,165,234,0) 80.61%), radial-gradient(10.394px 10.394px at 57.47% 35.67%, rgb(197,143,241) 10.32%, rgb(157,80,238) 32.29%, rgb(123,48,234) 51.04%, rgb(203,82,133) 78.13%)",
      }} />
      <svg width={22.105} height={20} viewBox="0 0 22.105 20" fill="none" style={{
        position: "absolute",
        left: 20,
        top: 22,
        width: 22.105,
        height: 20,
      }}>
        <path d={"M 11.564 18.875 C 11.507 18.942 11.445 19.009 11.38 19.072 C 10.121 20.309 8.08 20.309 6.822 19.072 L 0.944 13.294 C -0.315 12.058 -0.315 10.053 0.944 8.816 C 2.202 7.579 4.24 7.579 5.499 8.816 L 8.141 11.413 C 8.448 11.714 8.957 11.669 9.205 11.319 L 16.236 1.361 C 16.72 0.676 17.429 0.237 18.196 0.073 C 19.038 -0.107 19.954 0.043 20.72 0.565 C 22.183 1.562 22.544 3.534 21.53 4.971 L 12.168 18.232 C 11.994 18.479 11.79 18.694 11.564 18.875 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 52,
      height: 52,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <svg width={45.507} height={45.508} viewBox="0 0 45.507 45.508" fill="none" style={{
        position: "absolute",
        left: 3.246,
        top: 3.246,
        width: 45.507,
        height: 45.508,
      }}>
        <path d={"M 34.716 15.848 C 38.53 22.454 36.266 30.902 29.66 34.716 C 23.054 38.53 14.606 36.266 10.792 29.66 C 9.534 27.481 8.937 25.101 8.939 22.754 L 8.923 22.754 C 8.935 22.62 8.941 22.484 8.941 22.347 C 8.941 19.879 6.94 17.879 4.472 17.879 C 2.004 17.879 0.004 19.879 0.004 22.347 C 0.004 22.484 0.01 22.62 0.022 22.754 L 0 22.754 C -0.004 26.62 0.979 30.539 3.052 34.129 C 3.099 34.21 3.146 34.291 3.194 34.372 C 3.214 34.408 3.234 34.445 3.255 34.481 C 3.309 34.574 3.366 34.666 3.426 34.754 C 9.745 44.945 22.924 48.476 33.492 42.81 C 33.588 42.764 33.683 42.713 33.777 42.659 C 33.813 42.638 33.849 42.617 33.884 42.595 C 33.966 42.549 34.047 42.503 34.129 42.456 C 45.01 36.174 48.738 22.26 42.456 11.379 C 42.409 11.297 42.361 11.216 42.313 11.136 C 42.294 11.099 42.273 11.063 42.253 11.027 C 42.198 10.933 42.141 10.842 42.081 10.753 C 35.635 0.357 22.049 -3.109 11.379 3.052 L 11.388 3.067 C 11.266 3.124 11.145 3.186 11.027 3.255 C 8.889 4.489 8.157 7.222 9.391 9.359 C 10.625 11.497 13.358 12.229 15.496 10.995 C 15.614 10.926 15.728 10.853 15.838 10.776 L 15.847 10.792 C 22.454 6.978 30.901 9.241 34.716 15.848 Z"} fill="currentColor" fillRule="evenodd" />
      </svg>
      <svg width={19.297} height={31.484} viewBox="0 0 19.297 31.484" fill="none" style={{
        position: "absolute",
        left: 26.813,
        top: 8.125,
        width: 19.297,
        height: 31.484,
        color: "rgb(239,226,105)",
      }}>
        <path d={"M 15.234 6.805 C 15.234 6.973 15.098 7.109 14.93 7.109 C 14.761 7.109 14.625 6.973 14.625 6.805 C 14.625 6.636 14.761 6.5 14.93 6.5 C 15.098 6.5 15.234 6.636 15.234 6.805 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 0.813 0.406 C 0.813 0.631 0.631 0.813 0.406 0.813 C 0.182 0.813 0 0.631 0 0.406 C 0 0.182 0.182 0 0.406 0 C 0.631 0 0.813 0.182 0.813 0.406 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 6.5 2.844 C 6.5 3.068 6.318 3.25 6.094 3.25 C 5.869 3.25 5.688 3.068 5.688 2.844 C 5.688 2.619 5.869 2.438 6.094 2.438 C 6.318 2.438 6.5 2.619 6.5 2.844 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 8.938 2.844 C 8.938 3.068 8.756 3.25 8.531 3.25 C 8.307 3.25 8.125 3.068 8.125 2.844 C 8.125 2.619 8.307 2.438 8.531 2.438 C 8.756 2.438 8.938 2.619 8.938 2.844 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 18.688 14.219 C 18.688 14.443 18.506 14.625 18.281 14.625 C 18.057 14.625 17.875 14.443 17.875 14.219 C 17.875 13.994 18.057 13.813 18.281 13.813 C 18.506 13.813 18.688 13.994 18.688 14.219 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 10.359 31.18 C 10.359 31.348 10.223 31.484 10.055 31.484 C 9.886 31.484 9.75 31.348 9.75 31.18 C 9.75 31.011 9.886 30.875 10.055 30.875 C 10.223 30.875 10.359 31.011 10.359 31.18 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 12.797 26.305 C 12.797 26.473 12.66 26.609 12.492 26.609 C 12.324 26.609 12.188 26.473 12.188 26.305 C 12.188 26.136 12.324 26 12.492 26 C 12.66 26 12.797 26.136 12.797 26.305 Z"} fill="currentColor" fillRule="nonzero" />
        <path d={"M 19.297 25.594 C 19.297 25.762 19.16 25.898 18.992 25.898 C 18.824 25.898 18.688 25.762 18.688 25.594 C 18.688 25.425 18.824 25.289 18.992 25.289 C 19.16 25.289 19.297 25.425 19.297 25.594 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 12.025,
        top: 5.931,
        width: 8.938,
        height: 8.938,
        borderRadius: "50%",
        background: "radial-gradient(6.395px 3.236px at 48.28% 100.00%, rgba(255,179,187,0.5) 0.00%, rgba(254,98,93,0) 100.00%), radial-gradient(7.933px 7.933px at 50.00% 50.00%, rgba(255,255,255,0) 44.60%, rgba(255,255,255,0.8) 84.14%), radial-gradient(3.152px 3.152px at 59.46% 26.43%, rgb(237,218,253) 0.00%, rgba(216,189,239,0.5784) 57.33%, rgba(188,149,220,0) 100.00%), radial-gradient(6.609px 6.609px at 58.14% 28.41%, rgb(218,176,242) 0.00%, rgba(209,165,234,0) 80.61%), radial-gradient(8.445px 8.445px at 57.47% 35.67%, rgb(197,143,241) 10.32%, rgb(157,80,238) 32.29%, rgb(123,48,234) 51.04%, rgb(203,82,133) 78.13%)",
      }} />
      <svg width={17.961} height={16.250} viewBox="0 0 17.961 16.250" fill="none" style={{
        position: "absolute",
        left: 16.25,
        top: 17.875,
        width: 17.961,
        height: 16.25,
      }}>
        <path d={"M 9.396 15.336 C 9.35 15.39 9.299 15.445 9.246 15.496 C 8.224 16.501 6.565 16.501 5.543 15.496 L 0.767 10.801 C -0.256 9.797 -0.256 8.168 0.767 7.163 C 1.789 6.158 3.445 6.158 4.468 7.163 L 6.615 9.273 C 6.864 9.518 7.278 9.481 7.479 9.197 L 13.192 1.106 C 13.585 0.55 14.161 0.193 14.784 0.059 C 15.468 -0.087 16.213 0.035 16.835 0.459 C 18.023 1.269 18.317 2.871 17.493 4.039 L 9.886 14.813 C 9.745 15.014 9.579 15.188 9.396 15.336 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=512, Theme=Light
    "size=512|theme=light": __body0,
    // figma: Size=384, Theme=Dark
    "size=384|theme=dark": __body1,
    // figma: Size=512, Theme=Dark
    "size=512|theme=dark": __body0,
    // figma: Size=384, Theme=Light
    "size=384|theme=light": __body1,
    // figma: Size=256, Theme=Light
    "size=256|theme=light": __body2,
    // figma: Size=192, Theme=Light
    "size=192|theme=light": __body3,
    // figma: Size=192, Theme=Dark
    "size=192|theme=dark": __body3,
    // figma: Size=128, Theme=Light
    "size=128|theme=light": __body4,
    // figma: Size=128, Theme=Dark
    "size=128|theme=dark": __body4,
    // figma: Size=256, Theme=Dark
    "size=256|theme=dark": __body2,
    // figma: Size=64, Theme=Light
    "size=64|theme=light": __body5,
    // figma: Size=52, Theme=Light
    "size=52|theme=light": __body6,
    // figma: Size=64, Theme=Dark
    "size=64|theme=dark": __body7,
    // figma: Size=52, Theme=Dark
    "size=52|theme=dark": __body8,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default CheckmarkProgress;
