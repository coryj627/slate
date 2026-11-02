import { __slateNorm } from './slateProps.jsx';
// figma node: 9121:7414 Image (12 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Image(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24","28","32","48"],"theme":["regular","filled"]});
  const props = { ..._p, size: _p.size ?? "16", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 36,
        height: 36,
      }}>
        <path d={"M 20 11.5 C 20 9.015 22.015 7 24.5 7 C 26.985 7 29 9.015 29 11.5 C 29 13.985 26.985 16 24.5 16 C 22.015 16 20 13.985 20 11.5 Z M 24.5 9.5 C 23.395 9.5 22.5 10.395 22.5 11.5 C 22.5 12.605 23.395 13.5 24.5 13.5 C 25.605 13.5 26.5 12.605 26.5 11.5 C 26.5 10.395 25.605 9.5 24.5 9.5 Z M 0 6.25 C 0 2.798 2.798 0 6.25 0 L 29.75 0 C 33.202 0 36 2.798 36 6.25 L 36 29.75 C 36 33.202 33.202 36 29.75 36 L 6.25 36 C 2.798 36 0 33.202 0 29.75 L 0 6.25 Z M 6.25 2.5 C 4.179 2.5 2.5 4.179 2.5 6.25 L 2.5 29.75 C 2.5 30.413 2.672 31.036 2.974 31.576 L 15.525 19.025 C 16.892 17.658 19.108 17.658 20.475 19.025 L 33.026 31.576 C 33.328 31.036 33.5 30.413 33.5 29.75 L 33.5 6.25 C 33.5 4.179 31.821 2.5 29.75 2.5 L 6.25 2.5 Z M 31.146 33.232 L 18.707 20.793 C 18.317 20.402 17.684 20.402 17.293 20.793 L 4.854 33.232 C 5.286 33.405 5.757 33.5 6.25 33.5 L 29.75 33.5 C 30.243 33.5 30.714 33.405 31.146 33.232 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 36,
        height: 36,
      }}>
        <path d={"M 6.25 0 C 2.798 0 0 2.798 0 6.25 L 0 29.75 C 0 31.106 0.432 32.361 1.165 33.385 L 15.525 19.025 C 16.892 17.658 19.108 17.658 20.475 19.025 L 34.835 33.385 C 35.568 32.361 36 31.106 36 29.75 L 36 6.25 C 36 2.798 33.202 0 29.75 0 L 6.25 0 Z M 28.5 11.5 C 28.5 13.709 26.709 15.5 24.5 15.5 C 22.291 15.5 20.5 13.709 20.5 11.5 C 20.5 9.291 22.291 7.5 24.5 7.5 C 26.709 7.5 28.5 9.291 28.5 11.5 Z M 33.002 35.088 L 18.707 20.793 C 18.317 20.402 17.684 20.402 17.293 20.793 L 2.998 35.088 C 3.945 35.667 5.059 36 6.25 36 L 29.75 36 C 30.941 36 32.055 35.667 33.002 35.088 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={22} height={22} viewBox="0 0 22 22" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 22,
        height: 22,
      }}>
        <path d={"M 0 3.75 C 0 1.679 1.679 0 3.75 0 L 18.25 0 C 20.321 0 22 1.679 22 3.75 L 22 18.25 C 22 20.321 20.321 22 18.25 22 L 3.75 22 C 1.679 22 0 20.321 0 18.25 L 0 3.75 Z M 3.75 1.5 C 2.507 1.5 1.5 2.507 1.5 3.75 L 1.5 18.25 C 1.5 18.611 1.585 18.952 1.736 19.254 L 9.602 11.567 C 10.379 10.808 11.621 10.808 12.398 11.567 L 20.264 19.255 C 20.415 18.952 20.5 18.611 20.5 18.25 L 20.5 3.75 C 20.5 2.507 19.493 1.5 18.25 1.5 L 3.75 1.5 Z M 19.185 20.297 L 11.349 12.64 C 11.155 12.45 10.845 12.45 10.65 12.64 L 2.815 20.297 C 3.1 20.427 3.416 20.5 3.75 20.5 L 18.25 20.5 C 18.583 20.5 18.9 20.427 19.185 20.297 Z M 15.5 8 C 14.948 8 14.5 7.552 14.5 7 C 14.5 6.448 14.948 6 15.5 6 C 16.052 6 16.5 6.448 16.5 7 C 16.5 7.552 16.052 8 15.5 8 Z M 15.5 9.5 C 16.881 9.5 18 8.381 18 7 C 18 5.619 16.881 4.5 15.5 4.5 C 14.119 4.5 13 5.619 13 7 C 13 8.381 14.119 9.5 15.5 9.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={22} height={22} viewBox="0 0 22 22" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 22,
        height: 22,
      }}>
        <path d={"M 0 3.75 C 0 1.679 1.679 0 3.75 0 L 18.25 0 C 20.321 0 22 1.679 22 3.75 L 22 18.25 C 22 19.021 21.767 19.738 21.368 20.334 L 12.398 11.567 C 11.621 10.808 10.379 10.808 9.602 11.567 L 0.632 20.334 C 0.233 19.738 0 19.021 0 18.25 L 0 3.75 Z M 1.698 21.389 C 2.288 21.775 2.993 22 3.75 22 L 18.25 22 C 19.007 22 19.712 21.775 20.302 21.389 L 11.349 12.64 C 11.155 12.45 10.845 12.45 10.651 12.64 L 1.698 21.389 Z M 17.75 7 C 17.75 5.757 16.743 4.75 15.5 4.75 C 14.257 4.75 13.25 5.757 13.25 7 C 13.25 8.243 14.257 9.25 15.5 9.25 C 16.743 9.25 17.75 8.243 17.75 7 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={18} height={18} viewBox="0 0 18 18" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 18,
        height: 18,
      }}>
        <path d={"M 14.75 0 C 16.545 0 18 1.455 18 3.25 L 18 14.75 C 18 16.545 16.545 18 14.75 18 L 3.25 18 C 1.455 18 0 16.545 0 14.75 L 0 3.25 C 0 1.455 1.455 0 3.25 0 L 14.75 0 Z M 15.331 16.401 L 9.525 10.715 C 9.26 10.455 8.85 10.432 8.559 10.644 L 8.475 10.715 L 2.668 16.401 C 2.85 16.465 3.046 16.5 3.25 16.5 L 14.75 16.5 C 14.953 16.5 15.149 16.465 15.331 16.401 L 9.525 10.715 L 15.331 16.401 Z M 14.75 1.5 L 3.25 1.5 C 2.284 1.5 1.5 2.284 1.5 3.25 L 1.5 14.75 C 1.5 14.958 1.536 15.158 1.603 15.344 L 7.426 9.643 C 8.259 8.827 9.567 8.789 10.446 9.527 L 10.574 9.643 L 16.396 15.345 C 16.463 15.159 16.5 14.959 16.5 14.75 L 16.5 3.25 C 16.5 2.284 15.716 1.5 14.75 1.5 Z M 12.252 3.5 C 13.496 3.5 14.504 4.508 14.504 5.752 C 14.504 6.996 13.496 8.004 12.252 8.004 C 11.008 8.004 10 6.996 10 5.752 C 10 4.508 11.008 3.5 12.252 3.5 Z M 12.252 5 C 11.837 5 11.5 5.337 11.5 5.752 C 11.5 6.167 11.837 6.504 12.252 6.504 C 12.667 6.504 13.004 6.167 13.004 5.752 C 13.004 5.337 12.667 5 12.252 5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={18} height={18} viewBox="0 0 18 18" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 18,
        height: 18,
      }}>
        <path d={"M 8.558 10.647 L 8.475 10.718 L 1.547 17.519 C 2.042 17.824 2.626 18 3.25 18 L 14.75 18 C 15.374 18 15.958 17.824 16.453 17.519 L 9.525 10.718 L 9.432 10.64 C 9.17 10.455 8.817 10.458 8.558 10.647 Z M 18 3.25 C 18 1.455 16.545 0 14.75 0 L 3.25 0 C 1.455 0 0 1.455 0 3.25 L 0 14.75 C 0 15.377 0.178 15.963 0.485 16.459 L 7.424 9.648 L 7.559 9.525 C 8.394 8.827 9.615 8.829 10.448 9.531 L 10.576 9.648 L 17.515 16.459 C 17.822 15.963 18 15.377 18 14.75 L 18 3.25 Z M 12.25 7.75 C 11.145 7.75 10.25 6.855 10.25 5.75 C 10.25 4.645 11.145 3.75 12.25 3.75 C 13.355 3.75 14.25 4.645 14.25 5.75 C 14.25 6.855 13.355 7.75 12.25 7.75 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 14,
        height: 14,
      }}>
        <path d={"M 11 4.5 C 11 5.328 10.328 6 9.5 6 C 8.672 6 8 5.328 8 4.5 C 8 3.672 8.672 3 9.5 3 C 10.328 3 11 3.672 11 4.5 Z M 10 4.5 C 10 4.224 9.776 4 9.5 4 C 9.224 4 9 4.224 9 4.5 C 9 4.776 9.224 5 9.5 5 C 9.776 5 10 4.776 10 4.5 Z M 0 3 C 0 1.343 1.343 0 3 0 L 11 0 C 12.657 0 14 1.343 14 3 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 Z M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 11.373 1.102 11.721 1.279 12.02 L 5.949 7.432 C 6.532 6.859 7.468 6.859 8.051 7.432 L 12.721 12.02 C 12.898 11.721 13 11.373 13 11 L 13 3 C 13 1.895 12.105 1 11 1 L 3 1 Z M 3 13 L 11 13 C 11.369 13 11.715 12.9 12.012 12.726 L 7.35 8.145 C 7.156 7.954 6.844 7.954 6.65 8.145 L 1.988 12.726 C 2.285 12.9 2.631 13 3 13 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 14,
        height: 14,
      }}>
        <path d={"M 3 0 C 1.343 0 0 1.343 0 3 L 0 11 C 0 11.65 0.206 12.251 0.557 12.742 L 5.948 7.435 C 6.531 6.86 7.469 6.86 8.052 7.435 L 13.443 12.742 C 13.794 12.251 14 11.65 14 11 L 14 3 C 14 1.343 12.657 0 11 0 L 3 0 Z M 3 14 C 2.354 14 1.755 13.796 1.265 13.448 L 6.649 8.147 C 6.844 7.955 7.156 7.955 7.351 8.147 L 12.735 13.448 C 12.245 13.796 11.646 14 11 14 L 3 14 Z M 9.5 5.75 C 8.81 5.75 8.25 5.19 8.25 4.5 C 8.25 3.81 8.81 3.25 9.5 3.25 C 10.19 3.25 10.75 3.81 10.75 4.5 C 10.75 5.19 10.19 5.75 9.5 5.75 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 12,
        height: 12,
      }}>
        <path d={"M 9.5 3.502 C 9.5 4.056 9.051 4.504 8.498 4.504 C 7.944 4.504 7.496 4.056 7.496 3.502 C 7.496 2.949 7.944 2.5 8.498 2.5 C 9.051 2.5 9.5 2.949 9.5 3.502 Z M 0 2.5 C 0 1.119 1.119 0 2.5 0 L 9.5 0 C 10.881 0 12 1.119 12 2.5 L 12 9.5 C 12 10.881 10.881 12 9.5 12 L 2.5 12 C 1.119 12 0 10.881 0 9.5 L 0 2.5 Z M 2.5 1 C 1.672 1 1 1.672 1 2.5 L 1 9.5 C 1 9.732 1.052 9.951 1.146 10.147 L 4.798 6.495 C 5.462 5.831 6.538 5.831 7.202 6.495 L 10.854 10.147 C 10.948 9.951 11 9.732 11 9.5 L 11 2.5 C 11 1.672 10.328 1 9.5 1 L 2.5 1 Z M 10.147 10.854 L 6.495 7.202 C 6.222 6.929 5.778 6.929 5.505 7.202 L 1.853 10.854 C 2.049 10.948 2.268 11 2.5 11 L 9.5 11 C 9.732 11 9.951 10.948 10.147 10.854 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 12,
        height: 12,
      }}>
        <path d={"M 2.5 0 C 1.119 0 0 1.119 0 2.5 L 0 9.5 C 0 10.01 0.152 10.484 0.414 10.879 L 4.798 6.495 C 5.462 5.831 6.538 5.831 7.202 6.495 L 11.586 10.879 C 11.848 10.483 12 10.01 12 9.5 L 12 2.5 C 12 1.119 10.881 0 9.5 0 L 2.5 0 Z M 9.5 3.502 C 9.5 4.056 9.051 4.504 8.498 4.504 C 7.944 4.504 7.496 4.056 7.496 3.502 C 7.496 2.949 7.944 2.5 8.498 2.5 C 9.051 2.5 9.5 2.949 9.5 3.502 Z M 10.879 11.586 L 6.495 7.202 C 6.222 6.929 5.778 6.929 5.505 7.202 L 1.121 11.586 C 1.517 11.848 1.99 12 2.5 12 L 9.5 12 C 10.01 12 10.483 11.848 10.879 11.586 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={26} height={26} viewBox="0 0 26 26" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 26,
        height: 26,
      }}>
        <path d={"M 15 8 C 15 6.067 16.567 4.5 18.5 4.5 C 20.433 4.5 22 6.067 22 8 C 22 9.933 20.433 11.5 18.5 11.5 C 16.567 11.5 15 9.933 15 8 Z M 18.5 6.5 C 17.672 6.5 17 7.172 17 8 C 17 8.828 17.672 9.5 18.5 9.5 C 19.328 9.5 20 8.828 20 8 C 20 7.172 19.328 6.5 18.5 6.5 Z M 0 4.5 C 0 2.015 2.015 0 4.5 0 L 21.5 0 C 23.985 0 26 2.015 26 4.5 L 26 21.5 C 26 23.985 23.985 26 21.5 26 L 4.5 26 C 2.015 26 0 23.985 0 21.5 L 0 4.5 Z M 4.5 2 C 3.119 2 2 3.119 2 4.5 L 2 21.5 C 2 21.789 2.049 22.067 2.139 22.325 L 11.232 13.232 C 12.209 12.256 13.792 12.256 14.768 13.232 L 23.861 22.325 C 23.951 22.067 24 21.789 24 21.5 L 24 4.5 C 24 3.119 22.881 2 21.5 2 L 4.5 2 Z M 22.499 23.792 L 13.354 14.646 C 13.158 14.451 12.842 14.451 12.647 14.646 L 3.501 23.792 C 3.807 23.926 4.145 24 4.5 24 L 21.5 24 C 21.855 24 22.193 23.926 22.499 23.792 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={26} height={26} viewBox="0 0 26 26" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 26,
        height: 26,
      }}>
        <path d={"M 4.5 0 C 2.015 0 0 2.015 0 4.5 L 0 21.5 C 0 22.349 0.235 23.143 0.644 23.821 L 11.232 13.232 C 12.209 12.256 13.792 12.256 14.768 13.232 L 25.356 23.821 C 25.765 23.143 26 22.349 26 21.5 L 26 4.5 C 26 2.015 23.985 0 21.5 0 L 4.5 0 Z M 21.5 8 C 21.5 9.657 20.157 11 18.5 11 C 16.843 11 15.5 9.657 15.5 8 C 15.5 6.343 16.843 5 18.5 5 C 20.157 5 21.5 6.343 21.5 8 Z M 23.97 25.262 L 13.354 14.646 C 13.158 14.451 12.842 14.451 12.647 14.646 L 2.031 25.262 C 2.739 25.729 3.588 26 4.5 26 L 21.5 26 C 22.412 26 23.261 25.729 23.97 25.262 Z"} fill="currentColor" fillRule="nonzero" />
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
export default Image;
