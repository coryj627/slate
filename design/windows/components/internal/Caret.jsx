import { __slateNorm } from './slateProps.jsx';
// figma node: 9000:133 Caret (40 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "direction=" + __venc(p.direction) + '|' + "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Caret(_p = {}) {
  _p = __slateNorm(_p, {"direction":["down","up","left","right","down right"],"size":["12","16","20","24"],"theme":["regular","filled"]});
  const props = { ..._p, direction: _p.direction ?? "down", size: _p.size ?? "12", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14.301} height={8.981} viewBox="0 0 14.301 8.981" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,4.849,16.981)",
        transformOrigin: "0 0",
        width: 14.301,
        height: 8.981,
      }}>
        <path d={"M 0.312 2.073 C -0.395 1.265 0.178 0 1.252 0 L 13.049 0 C 14.123 0 14.697 1.265 13.989 2.073 L 8.468 8.384 C 7.77 9.181 6.531 9.181 5.834 8.384 L 0.312 2.073 Z M 1.803 1.5 L 6.962 7.396 C 7.062 7.51 7.239 7.51 7.339 7.396 L 12.498 1.5 L 1.803 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14.301} height={8.981} viewBox="0 0 14.301 8.981" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,4.849,16.981)",
        transformOrigin: "0 0",
        width: 14.301,
        height: 8.981,
      }}>
        <path d={"M 1.252 0 C 0.178 0 -0.395 1.265 0.312 2.073 L 5.834 8.384 C 6.531 9.181 7.77 9.181 8.468 8.384 L 13.989 2.073 C 14.697 1.265 14.123 0 13.049 0 L 1.252 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10.411} height={6.908} viewBox="0 0 10.411 6.908" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,4.795,13.908)",
        transformOrigin: "0 0",
        width: 10.411,
        height: 6.908,
      }}>
        <path d={"M 0.224 1.628 C -0.304 0.974 0.161 0 1.002 0 L 9.409 0 C 10.249 0 10.715 0.974 10.187 1.628 L 6.372 6.351 C 5.772 7.094 4.639 7.094 4.038 6.351 L 0.224 1.628 Z M 9.409 1 L 1.002 1 L 4.816 5.723 C 5.016 5.97 5.394 5.97 5.594 5.723 L 9.409 1 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10.411} height={6.908} viewBox="0 0 10.411 6.908" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,4.795,13.908)",
        transformOrigin: "0 0",
        width: 10.411,
        height: 6.908,
      }}>
        <path d={"M 1.002 0 C 0.161 0 -0.304 0.974 0.224 1.628 L 4.038 6.351 C 4.639 7.094 5.772 7.094 6.372 6.351 L 10.187 1.628 C 10.715 0.974 10.249 0 9.409 0 L 1.002 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8.090} height={5.999} viewBox="0 0 8.090 5.999" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,3.955,10.999)",
        transformOrigin: "0 0",
        width: 8.09,
        height: 5.999,
      }}>
        <path d={"M 0.181 1.571 C -0.28 0.908 0.194 0 1.002 0 L 7.088 0 C 7.896 0 8.37 0.908 7.909 1.571 L 5.276 5.355 C 4.679 6.213 3.41 6.213 2.813 5.355 L 0.181 1.571 Z M 1.002 1 L 3.634 4.784 C 3.833 5.07 4.256 5.07 4.455 4.784 L 7.088 1 L 1.002 1 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8.090} height={5.999} viewBox="0 0 8.090 5.999" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,3.955,10.999)",
        transformOrigin: "0 0",
        width: 8.09,
        height: 5.999,
      }}>
        <path d={"M 1.002 0 C 0.194 0 -0.28 0.908 0.181 1.571 L 2.813 5.355 C 3.41 6.213 4.679 6.213 5.276 5.355 L 7.909 1.571 C 8.37 0.908 7.896 0 7.088 0 L 1.002 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6} height={4} viewBox="0 0 6 4" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(1,0,0,-1,3,8)",
        transformOrigin: "0 0",
        width: 6,
        height: 4,
      }}>
        <path d={"M 0.076 0.617 C 0.231 0.244 0.596 0 1 0 L 5 0 C 5.404 0 5.769 0.244 5.924 0.617 C 6.079 0.991 5.993 1.421 5.707 1.707 L 3.707 3.707 C 3.317 4.098 2.683 4.098 2.293 3.707 L 0.293 1.707 C 0.007 1.421 -0.079 0.991 0.076 0.617 Z M 1 1 L 2.823 2.823 C 2.921 2.921 3.079 2.921 3.177 2.823 L 5 1 L 1 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6} height={4} viewBox="0 0 6 4" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 4,
        width: 6,
        height: 4,
      }}>
        <path d={"M 0.076 3.383 C 0.231 3.756 0.596 4 1 4 L 5 4 C 5.404 4 5.769 3.756 5.924 3.383 C 6.079 3.009 5.993 2.579 5.707 2.293 L 3.707 0.293 C 3.317 -0.098 2.683 -0.098 2.293 0.293 L 0.293 2.293 C 0.007 2.579 -0.079 3.009 0.076 3.383 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.981} height={14.301} viewBox="0 0 8.981 14.301" fill="none" style={{
        position: "absolute",
        left: 9,
        top: 4.849,
        width: 8.981,
        height: 14.301,
      }}>
        <path d={"M 2.073 13.989 C 1.265 14.697 0 14.123 0 13.049 L 0 1.252 C 0 0.178 1.265 -0.395 2.073 0.312 L 8.384 5.834 C 9.181 6.531 9.181 7.77 8.384 8.468 L 2.073 13.989 Z M 1.5 12.498 L 7.396 7.339 C 7.51 7.239 7.51 7.062 7.396 6.962 L 1.5 1.803 L 1.5 12.498 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.981} height={14.301} viewBox="0 0 8.981 14.301" fill="none" style={{
        position: "absolute",
        left: 9,
        top: 4.849,
        width: 8.981,
        height: 14.301,
      }}>
        <path d={"M 0 13.049 C 0 14.123 1.265 14.697 2.073 13.989 L 8.384 8.468 C 9.181 7.77 9.181 6.531 8.384 5.834 L 2.073 0.312 C 1.265 -0.395 0 0.178 0 1.252 L 0 13.049 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.908} height={10.411} viewBox="0 0 6.908 10.411" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 4.795,
        width: 6.908,
        height: 10.411,
      }}>
        <path d={"M 1.628 10.187 C 0.974 10.715 0 10.249 0 9.409 L 0 1.002 C 0 0.161 0.974 -0.304 1.628 0.224 L 6.351 4.038 C 7.094 4.639 7.094 5.772 6.351 6.372 L 1.628 10.187 Z M 1 1.002 L 1 9.409 L 5.723 5.594 C 5.97 5.394 5.97 5.016 5.723 4.816 L 1 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.908} height={10.411} viewBox="0 0 6.908 10.411" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 4.795,
        width: 6.908,
        height: 10.411,
      }}>
        <path d={"M 0 9.409 C 0 10.249 0.974 10.715 1.628 10.187 L 6.351 6.372 C 7.094 5.772 7.094 4.639 6.351 4.038 L 1.628 0.224 C 0.974 -0.304 0 0.161 0 1.002 L 0 9.409 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.040} height={5.999} viewBox="0 0 8.040 5.999" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,-1,1,0,6,12.020)",
        transformOrigin: "0 0",
        width: 8.04,
        height: 5.999,
      }}>
        <path d={"M 0.18 1.571 C -0.279 0.908 0.193 0 0.996 0 L 7.044 0 C 7.847 0 8.319 0.908 7.86 1.571 L 5.244 5.355 C 4.651 6.213 3.389 6.213 2.796 5.355 L 0.18 1.571 Z M 0.996 1 L 3.612 4.784 C 3.81 5.07 4.23 5.07 4.428 4.784 L 7.044 1 L 0.996 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.040} height={5.999} viewBox="0 0 8.040 5.999" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,-1,1,0,6,12.020)",
        transformOrigin: "0 0",
        width: 8.04,
        height: 5.999,
      }}>
        <path d={"M 0.18 1.571 C -0.279 0.908 0.193 0 0.996 0 L 7.044 0 C 7.847 0 8.319 0.908 7.86 1.571 L 5.244 5.355 C 4.651 6.213 3.389 6.213 2.796 5.355 L 0.18 1.571 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={4.037} height={6.016} viewBox="0 0 4.037 6.016" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2.992,
        width: 4.037,
        height: 6.016,
      }}>
        <path d={"M 0 1.002 C 0 0.115 1.069 -0.333 1.702 0.289 L 3.739 2.295 C 4.137 2.687 4.137 3.329 3.739 3.721 L 1.702 5.727 C 1.069 6.349 0 5.901 0 5.014 L 0 1.002 Z M 3.037 3.008 L 1 1.002 L 1 5.014 L 3.037 3.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={4.037} height={6.016} viewBox="0 0 4.037 6.016" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2.992,
        width: 4.037,
        height: 6.016,
      }}>
        <path d={"M 1.702 0.289 C 1.069 -0.333 0 0.115 0 1.002 L 0 5.014 C 0 5.901 1.069 6.349 1.702 5.727 L 3.739 3.721 C 4.137 3.329 4.137 2.687 3.739 2.295 L 1.702 0.289 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.981} height={14.301} viewBox="0 0 8.981 14.301" fill="none" style={{
        position: "absolute",
        left: 6.019,
        top: 4.849,
        width: 8.981,
        height: 14.301,
      }}>
        <path d={"M 6.908 13.989 C 7.717 14.697 8.981 14.123 8.981 13.049 L 8.981 1.252 C 8.981 0.178 7.717 -0.395 6.908 0.312 L 0.598 5.834 C -0.199 6.531 -0.199 7.77 0.598 8.468 L 6.908 13.989 Z M 7.481 12.498 L 1.585 7.339 C 1.472 7.239 1.472 7.062 1.585 6.962 L 7.481 1.803 L 7.481 12.498 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.981} height={14.301} viewBox="0 0 8.981 14.301" fill="none" style={{
        position: "absolute",
        left: 6.019,
        top: 4.849,
        width: 8.981,
        height: 14.301,
      }}>
        <path d={"M 8.981 13.049 C 8.981 14.123 7.717 14.697 6.908 13.989 L 0.598 8.468 C -0.199 7.77 -0.199 6.531 0.598 5.834 L 6.908 0.312 C 7.717 -0.395 8.981 0.178 8.981 1.252 L 8.981 13.049 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.908} height={10.411} viewBox="0 0 6.908 10.411" fill="none" style={{
        position: "absolute",
        left: 6.092,
        top: 4.795,
        width: 6.908,
        height: 10.411,
      }}>
        <path d={"M 5.28 10.187 C 5.934 10.715 6.908 10.249 6.908 9.409 L 6.908 1.002 C 6.908 0.161 5.934 -0.304 5.28 0.224 L 0.557 4.038 C -0.186 4.639 -0.186 5.772 0.557 6.372 L 5.28 10.187 Z M 5.908 1.002 L 5.908 9.409 L 1.186 5.594 C 0.938 5.394 0.938 5.016 1.186 4.816 L 5.908 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.908} height={10.411} viewBox="0 0 6.908 10.411" fill="none" style={{
        position: "absolute",
        left: 6.092,
        top: 4.795,
        width: 6.908,
        height: 10.411,
      }}>
        <path d={"M 6.908 9.409 C 6.908 10.249 5.934 10.715 5.28 10.187 L 0.557 6.372 C -0.186 5.772 -0.186 4.639 0.557 4.038 L 5.28 0.224 C 5.934 -0.304 6.908 0.161 6.908 1.002 L 6.908 9.409 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={5.999} height={8.090} viewBox="0 0 5.999 8.090" fill="none" style={{
        position: "absolute",
        left: 5.001,
        top: 3.955,
        width: 5.999,
        height: 8.09,
      }}>
        <path d={"M 4.428 7.909 C 5.091 8.37 5.999 7.896 5.999 7.088 L 5.999 1.002 C 5.999 0.194 5.091 -0.28 4.428 0.181 L 0.643 2.813 C -0.214 3.41 -0.214 4.679 0.643 5.276 L 4.428 7.909 Z M 4.999 7.088 L 1.214 4.455 C 0.929 4.256 0.929 3.833 1.214 3.634 L 4.999 1.002 L 4.999 7.088 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.040} height={5.999} viewBox="0 0 8.040 5.999" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,-1,-1,0,10.999,12.020)",
        transformOrigin: "0 0",
        width: 8.04,
        height: 5.999,
      }}>
        <path d={"M 0.18 1.571 C -0.279 0.908 0.193 0 0.996 0 L 7.044 0 C 7.847 0 8.319 0.908 7.86 1.571 L 5.244 5.355 C 4.651 6.213 3.389 6.213 2.796 5.355 L 0.18 1.571 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={4.037} height={6.016} viewBox="0 0 4.037 6.016" fill="none" style={{
        position: "absolute",
        left: 3.963,
        top: 2.992,
        width: 4.037,
        height: 6.016,
      }}>
        <path d={"M 4.037 1.002 C 4.037 0.115 2.968 -0.333 2.336 0.289 L 0.298 2.295 C -0.099 2.687 -0.099 3.329 0.298 3.721 L 2.336 5.727 C 2.968 6.349 4.037 5.901 4.037 5.014 L 4.037 1.002 Z M 1 3.008 L 3.037 1.002 L 3.037 5.014 L 1 3.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={4.037} height={6.016} viewBox="0 0 4.037 6.016" fill="none" style={{
        position: "absolute",
        left: 3.963,
        top: 2.992,
        width: 4.037,
        height: 6.016,
      }}>
        <path d={"M 2.336 0.289 C 2.968 -0.333 4.037 0.115 4.037 1.002 L 4.037 5.014 C 4.037 5.901 2.968 6.349 2.336 5.727 L 0.298 3.721 C -0.099 3.329 -0.099 2.687 0.298 2.295 L 2.336 0.289 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14.301} height={8.981} viewBox="0 0 14.301 8.981" fill="none" style={{
        position: "absolute",
        left: 4.849,
        top: 8,
        width: 14.301,
        height: 8.981,
      }}>
        <path d={"M 0.312 2.073 C -0.395 1.265 0.178 0 1.252 0 L 13.049 0 C 14.123 0 14.697 1.265 13.989 2.073 L 8.468 8.384 C 7.77 9.181 6.531 9.181 5.834 8.384 L 0.312 2.073 Z M 1.803 1.5 L 6.962 7.396 C 7.062 7.51 7.239 7.51 7.339 7.396 L 12.498 1.5 L 1.803 1.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14.301} height={8.981} viewBox="0 0 14.301 8.981" fill="none" style={{
        position: "absolute",
        left: 4.849,
        top: 8,
        width: 14.301,
        height: 8.981,
      }}>
        <path d={"M 1.252 0 C 0.178 0 -0.395 1.265 0.312 2.073 L 5.834 8.384 C 6.531 9.181 7.77 9.181 8.468 8.384 L 13.989 2.073 C 14.697 1.265 14.123 0 13.049 0 L 1.252 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.411} height={6.908} viewBox="0 0 10.411 6.908" fill="none" style={{
        position: "absolute",
        left: 4.795,
        top: 7,
        width: 10.411,
        height: 6.908,
      }}>
        <path d={"M 0.224 1.628 C -0.304 0.974 0.161 0 1.002 0 L 9.409 0 C 10.249 0 10.715 0.974 10.187 1.628 L 6.372 6.351 C 5.772 7.094 4.639 7.094 4.038 6.351 L 0.224 1.628 Z M 9.409 1 L 1.002 1 L 4.816 5.723 C 5.016 5.97 5.394 5.97 5.594 5.723 L 9.409 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.411} height={6.908} viewBox="0 0 10.411 6.908" fill="none" style={{
        position: "absolute",
        left: 4.795,
        top: 7,
        width: 10.411,
        height: 6.908,
      }}>
        <path d={"M 1.002 0 C 0.161 0 -0.304 0.974 0.224 1.628 L 4.038 6.351 C 4.639 7.094 5.772 7.094 6.372 6.351 L 10.187 1.628 C 10.715 0.974 10.249 0 9.409 0 L 1.002 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.090} height={5.999} viewBox="0 0 8.090 5.999" fill="none" style={{
        position: "absolute",
        left: 3.955,
        top: 5,
        width: 8.09,
        height: 5.999,
      }}>
        <path d={"M 0.181 1.571 C -0.28 0.908 0.194 0 1.002 0 L 7.088 0 C 7.896 0 8.37 0.908 7.909 1.571 L 5.276 5.355 C 4.679 6.213 3.41 6.213 2.813 5.355 L 0.181 1.571 Z M 1.002 1 L 3.634 4.784 C 3.833 5.07 4.256 5.07 4.455 4.784 L 7.088 1 L 1.002 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.090} height={5.999} viewBox="0 0 8.090 5.999" fill="none" style={{
        position: "absolute",
        left: 3.955,
        top: 5,
        width: 8.09,
        height: 5.999,
      }}>
        <path d={"M 1.002 0 C 0.194 0 -0.28 0.908 0.181 1.571 L 2.813 5.355 C 3.41 6.213 4.679 6.213 5.276 5.355 L 7.909 1.571 C 8.37 0.908 7.896 0 7.088 0 L 1.002 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6} height={4} viewBox="0 0 6 4" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 4,
        width: 6,
        height: 4,
      }}>
        <path d={"M 0.076 0.617 C 0.231 0.244 0.596 0 1 0 L 5 0 C 5.404 0 5.769 0.244 5.924 0.617 C 6.079 0.991 5.993 1.421 5.707 1.707 L 3.707 3.707 C 3.317 4.098 2.683 4.098 2.293 3.707 L 0.293 1.707 C 0.007 1.421 -0.079 0.991 0.076 0.617 Z M 1 1 L 2.823 2.823 C 2.921 2.921 3.079 2.921 3.177 2.823 L 5 1 L 1 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6} height={4} viewBox="0 0 6 4" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 4,
        width: 6,
        height: 4,
      }}>
        <path d={"M 0.076 0.617 C 0.231 0.244 0.596 0 1 0 L 5 0 C 5.404 0 5.769 0.244 5.924 0.617 C 6.079 0.991 5.993 1.421 5.707 1.707 L 3.707 3.707 C 3.317 4.098 2.683 4.098 2.293 3.707 L 0.293 1.707 C 0.007 1.421 -0.079 0.991 0.076 0.617 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12.045} height={12.045} viewBox="0 0 12.045 12.045" fill="none" style={{
        position: "absolute",
        left: 5.955,
        top: 5.955,
        width: 12.045,
        height: 12.045,
      }}>
        <path d={"M 10.545 1.856 L 1.856 10.545 L 10.295 10.545 C 10.433 10.545 10.545 10.433 10.545 10.295 L 10.545 1.856 Z M 9.912 0.369 C 10.699 -0.419 12.045 0.139 12.045 1.253 L 12.045 10.295 C 12.045 11.262 11.262 12.045 10.295 12.045 L 1.253 12.045 C 0.139 12.045 -0.419 10.699 0.369 9.912 L 9.912 0.369 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12.045} height={12.045} viewBox="0 0 12.045 12.045" fill="none" style={{
        position: "absolute",
        left: 5.955,
        top: 5.955,
        width: 12.045,
        height: 12.045,
      }}>
        <path d={"M 12.045 1.253 C 12.045 0.139 10.699 -0.419 9.912 0.369 L 0.369 9.912 C -0.419 10.699 0.139 12.045 1.253 12.045 L 10.295 12.045 C 11.262 12.045 12.045 11.262 12.045 10.295 L 12.045 1.253 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.045} height={10.045} viewBox="0 0 10.045 10.045" fill="none" style={{
        position: "absolute",
        left: 4.955,
        top: 4.955,
        width: 10.045,
        height: 10.045,
      }}>
        <path d={"M 8.338 0.295 C 8.968 -0.335 10.045 0.111 10.045 1.002 L 10.045 8.545 C 10.045 9.373 9.373 10.045 8.545 10.045 L 1.002 10.045 C 0.111 10.045 -0.335 8.968 0.295 8.338 L 8.338 0.295 Z M 9.045 1.002 L 1.002 9.045 L 8.545 9.045 C 8.821 9.045 9.045 8.821 9.045 8.545 L 9.045 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.045} height={10.045} viewBox="0 0 10.045 10.045" fill="none" style={{
        position: "absolute",
        left: 4.955,
        top: 4.955,
        width: 10.045,
        height: 10.045,
      }}>
        <path d={"M 10.045 1.002 C 10.045 0.111 8.968 -0.335 8.338 0.295 L 0.295 8.338 C -0.335 8.968 0.111 10.045 1.002 10.045 L 8.545 10.045 C 9.373 10.045 10.045 9.373 10.045 8.545 L 10.045 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.045} height={8.045} viewBox="0 0 8.045 8.045" fill="none" style={{
        position: "absolute",
        left: 3.955,
        top: 3.955,
        width: 8.045,
        height: 8.045,
      }}>
        <path d={"M 6.338 0.295 C 6.968 -0.335 8.045 0.111 8.045 1.002 L 8.045 6.545 C 8.045 7.373 7.373 8.045 6.545 8.045 L 1.002 8.045 C 0.111 8.045 -0.335 6.968 0.295 6.338 L 6.338 0.295 Z M 7.045 1.002 L 1.002 7.045 L 6.545 7.045 C 6.821 7.045 7.045 6.821 7.045 6.545 L 7.045 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.045} height={8.045} viewBox="0 0 8.045 8.045" fill="none" style={{
        position: "absolute",
        left: 3.955,
        top: 3.955,
        width: 8.045,
        height: 8.045,
      }}>
        <path d={"M 8.045 1.002 C 8.045 0.111 6.968 -0.335 6.338 0.295 L 0.295 6.338 C -0.335 6.968 0.111 8.045 1.002 8.045 L 6.545 8.045 C 7.373 8.045 8.045 7.373 8.045 6.545 L 8.045 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.045} height={6.045} viewBox="0 0 6.045 6.045" fill="none" style={{
        position: "absolute",
        left: 2.96,
        top: 2.96,
        width: 6.045,
        height: 6.045,
      }}>
        <path d={"M 4.338 0.295 C 4.968 -0.335 6.045 0.111 6.045 1.002 L 6.045 4.545 C 6.045 5.373 5.373 6.045 4.545 6.045 L 1.002 6.045 C 0.111 6.045 -0.335 4.968 0.295 4.338 L 4.338 0.295 Z M 5.045 1.002 L 1.002 5.045 L 4.545 5.045 C 4.821 5.045 5.045 4.821 5.045 4.545 L 5.045 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.045} height={6.045} viewBox="0 0 6.045 6.045" fill="none" style={{
        position: "absolute",
        left: 2.96,
        top: 2.96,
        width: 6.045,
        height: 6.045,
      }}>
        <path d={"M 6.045 1.002 C 6.045 0.111 4.968 -0.335 4.338 0.295 L 0.295 4.338 C -0.335 4.968 0.111 6.045 1.002 6.045 L 4.545 6.045 C 5.373 6.045 6.045 5.373 6.045 4.545 L 6.045 1.002 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Direction=Up, Size=24, Theme=Regular
    "direction=up|size=24|theme=regular": __body0,
    // figma: Direction=Up, Size=24, Theme=Filled
    "direction=up|size=24|theme=filled": __body1,
    // figma: Direction=Up, Size=20, Theme=Regular
    "direction=up|size=20|theme=regular": __body2,
    // figma: Direction=Up, Size=20, Theme=Filled
    "direction=up|size=20|theme=filled": __body3,
    // figma: Direction=Up, Size=16, Theme=Regular
    "direction=up|size=16|theme=regular": __body4,
    // figma: Direction=Up, Size=16, Theme=Filled
    "direction=up|size=16|theme=filled": __body5,
    // figma: Direction=Up, Size=12, Theme=Regular
    "direction=up|size=12|theme=regular": __body6,
    // figma: Direction=Up, Size=12, Theme=Filled
    "direction=up|size=12|theme=filled": __body7,
    // figma: Direction=Right, Size=24, Theme=Regular
    "direction=right|size=24|theme=regular": __body8,
    // figma: Direction=Right, Size=24, Theme=Filled
    "direction=right|size=24|theme=filled": __body9,
    // figma: Direction=Right, Size=20, Theme=Regular
    "direction=right|size=20|theme=regular": __body10,
    // figma: Direction=Right, Size=20, Theme=Filled
    "direction=right|size=20|theme=filled": __body11,
    // figma: Direction=Right, Size=16, Theme=Regular
    "direction=right|size=16|theme=regular": __body12,
    // figma: Direction=Right, Size=16, Theme=Filled
    "direction=right|size=16|theme=filled": __body13,
    // figma: Direction=Right, Size=12, Theme=Regular
    "direction=right|size=12|theme=regular": __body14,
    // figma: Direction=Right, Size=12, Theme=Filled
    "direction=right|size=12|theme=filled": __body15,
    // figma: Direction=Left, Size=24, Theme=Regular
    "direction=left|size=24|theme=regular": __body16,
    // figma: Direction=Left, Size=24, Theme=Filled
    "direction=left|size=24|theme=filled": __body17,
    // figma: Direction=Left, Size=20, Theme=Regular
    "direction=left|size=20|theme=regular": __body18,
    // figma: Direction=Left, Size=20, Theme=Filled
    "direction=left|size=20|theme=filled": __body19,
    // figma: Direction=Left, Size=16, Theme=Regular
    "direction=left|size=16|theme=regular": __body20,
    // figma: Direction=Left, Size=16, Theme=Filled
    "direction=left|size=16|theme=filled": __body21,
    // figma: Direction=Left, Size=12, Theme=Regular
    "direction=left|size=12|theme=regular": __body22,
    // figma: Direction=Left, Size=12, Theme=Filled
    "direction=left|size=12|theme=filled": __body23,
    // figma: Direction=Down, Size=24, Theme=Regular
    "direction=down|size=24|theme=regular": __body24,
    // figma: Direction=Down, Size=24, Theme=Filled
    "direction=down|size=24|theme=filled": __body25,
    // figma: Direction=Down, Size=20, Theme=Regular
    "direction=down|size=20|theme=regular": __body26,
    // figma: Direction=Down, Size=20, Theme=Filled
    "direction=down|size=20|theme=filled": __body27,
    // figma: Direction=Down, Size=16, Theme=Regular
    "direction=down|size=16|theme=regular": __body28,
    // figma: Direction=Down, Size=16, Theme=Filled
    "direction=down|size=16|theme=filled": __body29,
    // figma: Direction=Down, Size=12, Theme=Regular
    "direction=down|size=12|theme=regular": __body30,
    // figma: Direction=Down, Size=12, Theme=Filled
    "direction=down|size=12|theme=filled": __body31,
    // figma: Direction=Down Right, Size=24, Theme=Regular
    "direction=down right|size=24|theme=regular": __body32,
    // figma: Direction=Down Right, Size=24, Theme=Filled
    "direction=down right|size=24|theme=filled": __body33,
    // figma: Direction=Down Right, Size=20, Theme=Regular
    "direction=down right|size=20|theme=regular": __body34,
    // figma: Direction=Down Right, Size=20, Theme=Filled
    "direction=down right|size=20|theme=filled": __body35,
    // figma: Direction=Down Right, Size=16, Theme=Regular
    "direction=down right|size=16|theme=regular": __body36,
    // figma: Direction=Down Right, Size=16, Theme=Filled
    "direction=down right|size=16|theme=filled": __body37,
    // figma: Direction=Down Right, Size=12, Theme=Regular
    "direction=down right|size=12|theme=regular": __body38,
    // figma: Direction=Down Right, Size=12, Theme=Filled
    "direction=down right|size=12|theme=filled": __body39,
  };
  return (__impls[__vkey(props)] ?? __body30)();
}
export default Caret;
