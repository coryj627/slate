import { __slateNorm } from './slateProps.jsx';
// figma node: 9000:3451 Dismiss (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Dismiss(_p = {}) {
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
      <svg width={19.500} height={19.500} viewBox="0 0 19.500 19.500" fill="none" style={{
        position: "absolute",
        left: 4.25,
        top: 4.25,
        width: 19.5,
        height: 19.5,
      }}>
        <path d={"M 18.22 0.22 C 18.513 -0.073 18.987 -0.073 19.28 0.22 C 19.573 0.513 19.573 0.987 19.28 1.28 L 10.811 9.75 L 19.28 18.22 C 19.573 18.513 19.573 18.987 19.28 19.28 C 18.987 19.573 18.513 19.573 18.22 19.28 L 9.75 10.811 L 1.28 19.28 C 0.987 19.573 0.513 19.573 0.22 19.28 C -0.073 18.987 -0.073 18.513 0.22 18.22 L 8.689 9.75 L 0.22 1.28 C -0.073 0.987 -0.073 0.513 0.22 0.22 C 0.513 -0.073 0.987 -0.073 1.28 0.22 L 9.75 8.689 L 18.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 20,
        height: 20,
      }}>
        <path d={"M 18.293 0.293 C 18.683 -0.098 19.317 -0.098 19.707 0.293 C 20.098 0.683 20.098 1.317 19.707 1.707 L 11.414 10 L 19.707 18.293 C 20.098 18.683 20.098 19.317 19.707 19.707 C 19.317 20.098 18.683 20.098 18.293 19.707 L 10 11.414 L 1.707 19.707 C 1.317 20.098 0.683 20.098 0.293 19.707 C -0.098 19.317 -0.098 18.683 0.293 18.293 L 8.586 10 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 10 8.586 L 18.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={15.500} height={15.500} viewBox="0 0 15.500 15.500" fill="none" style={{
        position: "absolute",
        left: 4.25,
        top: 4.25,
        width: 15.5,
        height: 15.5,
      }}>
        <path d={"M 0.147 0.304 L 0.22 0.22 C 0.486 -0.047 0.903 -0.071 1.196 0.147 L 1.28 0.22 L 7.75 6.689 L 14.22 0.22 C 14.513 -0.073 14.987 -0.073 15.28 0.22 C 15.573 0.513 15.573 0.987 15.28 1.28 L 8.811 7.75 L 15.28 14.22 C 15.547 14.486 15.571 14.903 15.353 15.196 L 15.28 15.28 C 15.014 15.547 14.597 15.571 14.304 15.353 L 14.22 15.28 L 7.75 8.811 L 1.28 15.28 C 0.987 15.573 0.513 15.573 0.22 15.28 C -0.073 14.987 -0.073 14.513 0.22 14.22 L 6.689 7.75 L 0.22 1.28 C -0.047 1.014 -0.071 0.597 0.147 0.304 L 0.22 0.22 L 0.147 0.304 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 16,
        height: 16,
      }}>
        <path d={"M 0.21 0.387 L 0.293 0.293 C 0.653 -0.068 1.221 -0.095 1.613 0.21 L 1.707 0.293 L 8 6.585 L 14.293 0.293 C 14.683 -0.098 15.317 -0.098 15.707 0.293 C 16.098 0.683 16.098 1.317 15.707 1.707 L 9.415 8 L 15.707 14.293 C 16.068 14.653 16.095 15.221 15.79 15.613 L 15.707 15.707 C 15.347 16.068 14.779 16.095 14.387 15.79 L 14.293 15.707 L 8 9.415 L 1.707 15.707 C 1.317 16.098 0.683 16.098 0.293 15.707 C -0.098 15.317 -0.098 14.683 0.293 14.293 L 6.585 8 L 0.293 1.707 C -0.068 1.347 -0.095 0.779 0.21 0.387 L 0.293 0.293 L 0.21 0.387 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 12,
        height: 12,
      }}>
        <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.500} height={12.500} viewBox="0 0 12.500 12.500" fill="none" style={{
        position: "absolute",
        left: 3.75,
        top: 3.75,
        width: 12.5,
        height: 12.5,
      }}>
        <path d={"M 0.147 0.304 L 0.22 0.22 C 0.486 -0.047 0.903 -0.071 1.196 0.147 L 1.28 0.22 L 6.25 5.189 L 11.22 0.22 C 11.486 -0.047 11.903 -0.071 12.196 0.147 L 12.28 0.22 C 12.547 0.486 12.571 0.903 12.353 1.196 L 12.28 1.28 L 7.311 6.25 L 12.28 11.22 C 12.547 11.486 12.571 11.903 12.353 12.196 L 12.28 12.28 C 12.014 12.547 11.597 12.571 11.304 12.353 L 11.22 12.28 L 6.25 7.311 L 1.28 12.28 C 1.014 12.547 0.597 12.571 0.304 12.353 L 0.22 12.28 C -0.047 12.014 -0.071 11.597 0.147 11.304 L 0.22 11.22 L 5.189 6.25 L 0.22 1.28 C -0.047 1.014 -0.071 0.597 0.147 0.304 L 0.22 0.22 L 0.147 0.304 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={11} height={11} viewBox="0 0 11 11" fill="none" style={{
        position: "absolute",
        left: 2.5,
        top: 2.5,
        width: 11,
        height: 11,
      }}>
        <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 5.5 4.793 L 10.146 0.146 C 10.342 -0.049 10.658 -0.049 10.854 0.146 C 11.049 0.342 11.049 0.658 10.854 0.854 L 6.207 5.5 L 10.854 10.146 C 11.027 10.32 11.046 10.589 10.911 10.784 L 10.854 10.854 C 10.68 11.027 10.411 11.046 10.216 10.911 L 10.146 10.854 L 5.5 6.207 L 0.854 10.854 C 0.658 11.049 0.342 11.049 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 4.793 5.5 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={11.500} height={11.500} viewBox="0 0 11.500 11.500" fill="none" style={{
        position: "absolute",
        left: 2.25,
        top: 2.25,
        width: 11.5,
        height: 11.5,
      }}>
        <path d={"M 0.147 0.304 L 0.22 0.22 C 0.486 -0.047 0.903 -0.071 1.196 0.147 L 1.28 0.22 L 5.75 4.689 L 10.22 0.22 C 10.513 -0.073 10.987 -0.073 11.28 0.22 C 11.573 0.513 11.573 0.987 11.28 1.28 L 6.811 5.75 L 11.28 10.22 C 11.547 10.486 11.571 10.903 11.353 11.196 L 11.28 11.28 C 11.014 11.547 10.597 11.571 10.304 11.353 L 10.22 11.28 L 5.75 6.811 L 1.28 11.28 C 0.987 11.573 0.513 11.573 0.22 11.28 C -0.073 10.987 -0.073 10.513 0.22 10.22 L 4.689 5.75 L 0.22 1.28 C -0.047 1.014 -0.071 0.597 0.147 0.304 L 0.22 0.22 L 0.147 0.304 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 8,
        height: 8,
      }}>
        <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.707 4 L 7.854 7.146 C 8.027 7.32 8.046 7.589 7.911 7.784 L 7.854 7.854 C 7.68 8.027 7.411 8.046 7.216 7.911 L 7.146 7.854 L 4 4.707 L 0.854 7.854 C 0.658 8.049 0.342 8.049 0.146 7.854 C -0.049 7.658 -0.049 7.342 0.146 7.146 L 3.293 4 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.500} height={8.500} viewBox="0 0 8.500 8.500" fill="none" style={{
        position: "absolute",
        left: 1.75,
        top: 1.75,
        width: 8.5,
        height: 8.5,
      }}>
        <path d={"M 0.147 0.304 L 0.22 0.22 C 0.486 -0.047 0.903 -0.071 1.196 0.147 L 1.28 0.22 L 4.25 3.189 L 7.22 0.22 C 7.513 -0.073 7.987 -0.073 8.28 0.22 C 8.573 0.513 8.573 0.987 8.28 1.28 L 5.311 4.25 L 8.28 7.22 C 8.547 7.486 8.571 7.903 8.353 8.196 L 8.28 8.28 C 8.014 8.547 7.597 8.571 7.304 8.353 L 7.22 8.28 L 4.25 5.311 L 1.28 8.28 C 0.987 8.573 0.513 8.573 0.22 8.28 C -0.073 7.987 -0.073 7.513 0.22 7.22 L 3.189 4.25 L 0.22 1.28 C -0.047 1.014 -0.071 0.597 0.147 0.304 L 0.22 0.22 L 0.147 0.304 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={23.997} height={23.997} viewBox="0 0 23.997 23.997" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 23.997,
        height: 23.997,
      }}>
        <path d={"M 22.29 0.293 C 22.681 -0.098 23.314 -0.098 23.704 0.293 C 24.095 0.683 24.095 1.317 23.704 1.707 L 13.413 11.999 L 23.704 22.29 C 24.095 22.681 24.095 23.314 23.704 23.704 C 23.314 24.095 22.681 24.095 22.29 23.704 L 11.999 13.413 L 1.707 23.704 C 1.317 24.095 0.683 24.095 0.293 23.704 C -0.098 23.314 -0.098 22.681 0.293 22.29 L 10.584 11.999 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 C 0.684 -0.098 1.317 -0.098 1.707 0.293 L 11.999 10.584 L 22.29 0.293 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={24.497} height={24.497} viewBox="0 0 24.497 24.497" fill="none" style={{
        position: "absolute",
        left: 3.75,
        top: 3.75,
        width: 24.497,
        height: 24.497,
      }}>
        <path d={"M 22.363 0.366 C 22.851 -0.122 23.643 -0.122 24.131 0.366 C 24.619 0.854 24.619 1.646 24.131 2.134 L 14.016 12.249 L 24.131 22.363 C 24.619 22.851 24.619 23.643 24.131 24.131 C 23.643 24.619 22.852 24.619 22.363 24.131 L 12.249 14.016 L 2.134 24.131 C 1.646 24.619 0.854 24.619 0.366 24.131 C -0.122 23.643 -0.122 22.851 0.366 22.363 L 10.481 12.249 L 0.366 2.134 C -0.122 1.646 -0.122 0.854 0.366 0.366 C 0.854 -0.122 1.646 -0.122 2.134 0.366 L 12.249 10.481 L 22.363 0.366 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={36.000} height={36.000} viewBox="0 0 36.000 36.000" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 36,
        height: 36,
      }}>
        <path d={"M 2.561 0.439 C 1.975 -0.146 1.025 -0.146 0.439 0.439 C -0.146 1.025 -0.146 1.975 0.439 2.561 L 15.879 18 L 0.44 33.439 C -0.146 34.025 -0.146 34.975 0.44 35.56 C 1.025 36.146 1.975 36.146 2.561 35.56 L 18 20.121 L 33.439 35.56 C 34.024 36.146 34.974 36.146 35.56 35.56 C 36.146 34.974 36.146 34.024 35.56 33.439 L 20.121 18 L 35.56 2.561 C 36.146 1.975 36.146 1.026 35.56 0.44 C 34.974 -0.146 34.025 -0.146 33.439 0.44 L 18 15.879 L 2.561 0.439 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={35.500} height={35.500} viewBox="0 0 35.500 35.500" fill="none" style={{
        position: "absolute",
        left: 6.25,
        top: 6.25,
        width: 35.5,
        height: 35.5,
      }}>
        <path d={"M 2.134 0.366 C 1.646 -0.122 0.854 -0.122 0.366 0.366 C -0.122 0.854 -0.122 1.646 0.366 2.134 L 15.982 17.75 L 0.366 33.366 C -0.122 33.854 -0.122 34.645 0.366 35.134 C 0.855 35.622 1.646 35.622 2.134 35.134 L 17.75 19.518 L 33.365 35.133 C 33.854 35.621 34.645 35.621 35.133 35.133 C 35.621 34.645 35.621 33.854 35.133 33.365 L 19.518 17.75 L 35.133 2.134 C 35.622 1.646 35.622 0.855 35.133 0.367 C 34.645 -0.122 33.854 -0.122 33.366 0.367 L 17.75 15.982 L 2.134 0.366 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 24,
        height: 24,
      }}>
        <path d={"M 23.854 0.854 C 24.049 0.658 24.049 0.342 23.854 0.146 C 23.658 -0.049 23.342 -0.049 23.146 0.146 L 12 11.293 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 11.293 12 L 0.146 23.146 C -0.049 23.342 -0.049 23.658 0.146 23.854 C 0.342 24.049 0.658 24.049 0.854 23.854 L 12 12.707 L 23.146 23.854 C 23.342 24.049 23.658 24.049 23.854 23.854 C 24.049 23.658 24.049 23.342 23.854 23.146 L 12.707 12 L 23.854 0.854 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body8,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body9,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body10,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body11,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body12,
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body13,
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body14,
  };
  return (__impls[__vkey(props)] ?? __body8)();
}
export default Dismiss;
