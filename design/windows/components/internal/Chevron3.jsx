import { __slateNorm } from './slateProps.jsx';
// figma node: 9159:850 Chevron (56 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "direction=" + __venc(p.direction) + '|' + "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Chevron3(_p = {}) {
  _p = __slateNorm(_p, {"direction":["down","up","left","right"],"size":["12","16","20","24","28","32","48"],"theme":["regular","filled"]});
  const props = { ..._p, direction: _p.direction ?? "down", size: _p.size ?? "12", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={32} height={17.250} viewBox="0 0 32 17.250" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-1,0,0,-1,40,32.250)",
        transformOrigin: "0 0",
        width: 32,
        height: 17.25,
      }}>
        <path d={"M 0.366 0.366 C 0.854 -0.122 1.646 -0.122 2.134 0.366 L 16 14.232 L 29.866 0.366 C 30.354 -0.122 31.146 -0.122 31.634 0.366 C 32.122 0.854 32.122 1.646 31.634 2.134 L 16.884 16.884 C 16.396 17.372 15.604 17.372 15.116 16.884 L 0.366 2.134 C -0.122 1.646 -0.122 0.854 0.366 0.366 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={32} height={17.500} viewBox="0 0 32 17.500" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-1,0,0,-1,40,32.500)",
        transformOrigin: "0 0",
        width: 32,
        height: 17.5,
      }}>
        <path d={"M 0.439 0.439 C 1.025 -0.146 1.975 -0.146 2.561 0.439 L 16 13.879 L 29.439 0.439 C 30.025 -0.146 30.975 -0.146 31.561 0.439 C 32.146 1.025 32.146 1.975 31.561 2.561 L 17.061 17.061 C 16.475 17.646 15.525 17.646 14.939 17.061 L 0.439 2.561 C -0.146 1.975 -0.146 1.025 0.439 0.439 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={10.750} viewBox="0 0 20 10.750" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-1,0,0,-1,24,18.750)",
        transformOrigin: "0 0",
        width: 20,
        height: 10.75,
      }}>
        <path d={"M 0.22 0.22 C 0.513 -0.073 0.987 -0.073 1.28 0.22 L 10 8.939 L 18.72 0.22 C 19.013 -0.073 19.487 -0.073 19.78 0.22 C 20.073 0.513 20.073 0.987 19.78 1.28 L 10.53 10.53 C 10.237 10.823 9.763 10.823 9.47 10.53 L 0.22 1.28 C -0.073 0.987 -0.073 0.513 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={11} viewBox="0 0 20 11" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-1,0,0,-1,24,19)",
        transformOrigin: "0 0",
        width: 20,
        height: 11,
      }}>
        <path d={"M 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 10 8.586 L 18.293 0.293 C 18.683 -0.098 19.317 -0.098 19.707 0.293 C 20.098 0.683 20.098 1.317 19.707 1.707 L 10.707 10.707 C 10.317 11.098 9.683 11.098 9.293 10.707 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={8.750} viewBox="0 0 16 8.750" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 7,
        width: 16,
        height: 8.75,
      }}>
        <path d={"M 0.22 8.53 C 0.513 8.823 0.987 8.823 1.28 8.53 L 8 1.811 L 14.72 8.53 C 15.013 8.823 15.487 8.823 15.78 8.53 C 16.073 8.237 16.073 7.763 15.78 7.47 L 8.53 0.22 C 8.237 -0.073 7.763 -0.073 7.47 0.22 L 0.22 7.47 C -0.073 7.763 -0.073 8.237 0.22 8.53 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={9} viewBox="0 0 16 9" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 7,
        width: 16,
        height: 9,
      }}>
        <path d={"M 0.293 8.707 C 0.683 9.098 1.317 9.098 1.707 8.707 L 8 2.414 L 14.293 8.707 C 14.683 9.098 15.317 9.098 15.707 8.707 C 16.098 8.317 16.098 7.683 15.707 7.293 L 8.707 0.293 C 8.317 -0.098 7.683 -0.098 7.293 0.293 L 0.293 7.293 C -0.098 7.683 -0.098 8.317 0.293 8.707 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.001} height={6.499} viewBox="0 0 12.001 6.499" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 6.001,
        width: 12.001,
        height: 6.499,
      }}>
        <path d={"M 0.147 6.353 C -0.049 6.158 -0.049 5.842 0.146 5.646 L 5.611 0.162 C 5.826 -0.054 6.175 -0.054 6.39 0.162 L 11.855 5.646 C 12.05 5.842 12.049 6.158 11.854 6.353 C 11.658 6.548 11.342 6.548 11.147 6.352 L 6 1.188 L 0.854 6.352 C 0.659 6.548 0.343 6.548 0.147 6.353 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.001} height={6.501} viewBox="0 0 12.001 6.501" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 5.999,
        width: 12.001,
        height: 6.501,
      }}>
        <path d={"M 0.207 6.269 C -0.079 5.969 -0.067 5.494 0.233 5.208 L 5.483 0.207 C 5.773 -0.069 6.228 -0.069 6.518 0.207 L 11.768 5.208 C 12.068 5.494 12.08 5.969 11.794 6.269 C 11.508 6.569 11.034 6.58 10.734 6.294 L 6 1.786 L 1.267 6.294 C 0.967 6.58 0.493 6.569 0.207 6.269 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={5.500} viewBox="0 0 10 5.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 10,
        height: 5.5,
      }}>
        <path d={"M 0.146 5.354 C 0.342 5.549 0.658 5.549 0.854 5.354 L 5 1.207 L 9.146 5.354 C 9.342 5.549 9.658 5.549 9.854 5.354 C 10.049 5.158 10.049 4.842 9.854 4.646 L 5.354 0.146 C 5.158 -0.049 4.842 -0.049 4.646 0.146 L 0.146 4.646 C -0.049 4.842 -0.049 5.158 0.146 5.354 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={5.500} viewBox="0 0 10 5.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 10,
        height: 5.5,
      }}>
        <path d={"M 0.2 5.26 C 0.482 5.564 0.957 5.581 1.26 5.3 L 5 1.773 L 8.74 5.3 C 9.043 5.581 9.518 5.564 9.8 5.26 C 10.081 4.957 10.064 4.482 9.76 4.2 L 5.51 0.2 C 5.223 -0.067 4.777 -0.067 4.49 0.2 L 0.24 4.2 C -0.064 4.482 -0.081 4.957 0.2 5.26 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 8,
        height: 4.5,
      }}>
        <path d={"M 0.146 4.354 C 0.342 4.549 0.658 4.549 0.854 4.354 L 4 1.207 L 7.146 4.354 C 7.342 4.549 7.658 4.549 7.854 4.354 C 8.049 4.158 8.049 3.842 7.854 3.646 L 4.354 0.146 C 4.158 -0.049 3.842 -0.049 3.646 0.146 L 0.146 3.646 C -0.049 3.842 -0.049 4.158 0.146 4.354 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8} height={4.750} viewBox="0 0 8 4.750" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 8,
        height: 4.75,
      }}>
        <path d={"M 0.22 4.53 C 0.513 4.823 0.987 4.823 1.28 4.53 L 4 1.811 L 6.72 4.53 C 7.013 4.823 7.487 4.823 7.78 4.53 C 8.073 4.237 8.073 3.763 7.78 3.47 L 4.53 0.22 C 4.237 -0.073 3.763 -0.073 3.47 0.22 L 0.22 3.47 C -0.073 3.763 -0.073 4.237 0.22 4.53 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={32} height={17.250} viewBox="0 0 32 17.250" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,-1,1,0,15.750,40)",
        transformOrigin: "0 0",
        width: 32,
        height: 17.25,
      }}>
        <path d={"M 0.366 0.366 C 0.854 -0.122 1.646 -0.122 2.134 0.366 L 16 14.232 L 29.866 0.366 C 30.354 -0.122 31.146 -0.122 31.634 0.366 C 32.122 0.854 32.122 1.646 31.634 2.134 L 16.884 16.884 C 16.396 17.372 15.604 17.372 15.116 16.884 L 0.366 2.134 C -0.122 1.646 -0.122 0.854 0.366 0.366 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={32} height={17.500} viewBox="0 0 32 17.500" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,-1,1,0,15.500,39.750)",
        transformOrigin: "0 0",
        width: 32,
        height: 17.5,
      }}>
        <path d={"M 0.439 0.439 C 1.025 -0.146 1.975 -0.146 2.561 0.439 L 16 13.879 L 29.439 0.439 C 30.025 -0.146 30.975 -0.146 31.561 0.439 C 32.146 1.025 32.146 1.975 31.561 2.561 L 17.061 17.061 C 16.475 17.646 15.525 17.646 14.939 17.061 L 0.439 2.561 C -0.146 1.975 -0.146 1.025 0.439 0.439 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.250} height={19} viewBox="0 0 10.250 19" fill="none" style={{
        position: "absolute",
        left: 9.75,
        top: 4,
        width: 10.25,
        height: 19,
      }}>
        <path d={"M 0.22 0.22 C 0.513 -0.073 0.987 -0.073 1.28 0.22 L 10.03 8.97 C 10.171 9.11 10.25 9.301 10.25 9.5 C 10.25 9.699 10.171 9.89 10.03 10.03 L 1.28 18.78 C 0.987 19.073 0.513 19.073 0.22 18.78 C -0.073 18.487 -0.073 18.013 0.22 17.72 L 8.439 9.5 L 0.22 1.28 C -0.073 0.987 -0.073 0.513 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.750} height={19.500} viewBox="0 0 10.750 19.500" fill="none" style={{
        position: "absolute",
        left: 9.25,
        top: 4,
        width: 10.75,
        height: 19.5,
      }}>
        <path d={"M 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 10.457 9.043 C 10.645 9.23 10.75 9.485 10.75 9.75 C 10.75 10.015 10.645 10.27 10.457 10.457 L 1.707 19.207 C 1.317 19.598 0.683 19.598 0.293 19.207 C -0.098 18.817 -0.098 18.183 0.293 17.793 L 8.336 9.75 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
        position: "absolute",
        left: 8.25,
        top: 4,
        width: 8.75,
        height: 16,
      }}>
        <path d={"M 0.22 0.22 C -0.073 0.513 -0.073 0.987 0.22 1.28 L 6.939 8 L 0.22 14.72 C -0.073 15.013 -0.073 15.487 0.22 15.78 C 0.513 16.073 0.987 16.073 1.28 15.78 L 8.53 8.53 C 8.823 8.237 8.823 7.763 8.53 7.47 L 1.28 0.22 C 0.987 -0.073 0.513 -0.073 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={9} height={16} viewBox="0 0 9 16" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 4,
        width: 9,
        height: 16,
      }}>
        <path d={"M 0.293 0.293 C -0.098 0.683 -0.098 1.317 0.293 1.707 L 6.586 8 L 0.293 14.293 C -0.098 14.683 -0.098 15.317 0.293 15.707 C 0.683 16.098 1.317 16.098 1.707 15.707 L 8.707 8.707 C 9.098 8.317 9.098 7.683 8.707 7.293 L 1.707 0.293 C 1.317 -0.098 0.683 -0.098 0.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={6.499} height={12.001} viewBox="0 0 6.499 12.001" fill="none" style={{
        position: "absolute",
        left: 7.5,
        top: 4,
        width: 6.499,
        height: 12.001,
      }}>
        <path d={"M 0.146 0.147 C 0.341 -0.049 0.657 -0.049 0.853 0.146 L 6.337 5.611 C 6.553 5.826 6.553 6.175 6.337 6.39 L 0.853 11.855 C 0.657 12.05 0.341 12.049 0.146 11.854 C -0.049 11.658 -0.049 11.342 0.147 11.147 L 5.312 6 L 0.147 0.854 C -0.049 0.659 -0.049 0.343 0.146 0.147 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={6.501} height={12.001} viewBox="0 0 6.501 12.001" fill="none" style={{
        position: "absolute",
        left: 7.5,
        top: 4,
        width: 6.501,
        height: 12.001,
      }}>
        <path d={"M 0.233 0.207 C 0.533 -0.079 1.007 -0.067 1.293 0.233 L 6.294 5.483 C 6.57 5.773 6.57 6.228 6.294 6.518 L 1.293 11.768 C 1.007 12.068 0.533 12.08 0.233 11.794 C -0.067 11.508 -0.079 11.034 0.207 10.734 L 4.716 6 L 0.207 1.267 C -0.079 0.967 -0.067 0.493 0.233 0.207 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={5.500} height={10} viewBox="0 0 5.500 10" fill="none" style={{
        position: "absolute",
        left: 5.5,
        top: 3,
        width: 5.5,
        height: 10,
      }}>
        <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 4.293 5 L 0.146 9.146 C -0.049 9.342 -0.049 9.658 0.146 9.854 C 0.342 10.049 0.658 10.049 0.854 9.854 L 5.354 5.354 C 5.549 5.158 5.549 4.842 5.354 4.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={5.500} height={10} viewBox="0 0 5.500 10" fill="none" style={{
        position: "absolute",
        left: 5.5,
        top: 3,
        width: 5.5,
        height: 10,
      }}>
        <path d={"M 0.24 0.2 C -0.064 0.482 -0.081 0.957 0.2 1.26 L 3.727 5 L 0.2 8.74 C -0.081 9.043 -0.064 9.518 0.24 9.8 C 0.543 10.081 1.018 10.064 1.3 9.76 L 5.3 5.51 C 5.567 5.223 5.567 4.777 5.3 4.49 L 1.3 0.24 C 1.018 -0.064 0.543 -0.081 0.24 0.2 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
        position: "absolute",
        left: 4.5,
        top: 2,
        width: 4.5,
        height: 8,
      }}>
        <path d={"M 0.146 0.146 C -0.049 0.342 -0.049 0.658 0.146 0.854 L 3.293 4 L 0.146 7.146 C -0.049 7.342 -0.049 7.658 0.146 7.854 C 0.342 8.049 0.658 8.049 0.854 7.854 L 4.354 4.354 C 4.549 4.158 4.549 3.842 4.354 3.646 L 0.854 0.146 C 0.658 -0.049 0.342 -0.049 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={4.750} height={8} viewBox="0 0 4.750 8" fill="none" style={{
        position: "absolute",
        left: 4.25,
        top: 2,
        width: 4.75,
        height: 8,
      }}>
        <path d={"M 0.22 0.22 C -0.073 0.513 -0.073 0.987 0.22 1.28 L 2.939 4 L 0.22 6.72 C -0.073 7.013 -0.073 7.487 0.22 7.78 C 0.513 8.073 0.987 8.073 1.28 7.78 L 4.53 4.53 C 4.823 4.237 4.823 3.763 4.53 3.47 L 1.28 0.22 C 0.987 -0.073 0.513 -0.073 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={32} height={17.250} viewBox="0 0 32 17.250" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,32.250,8)",
        transformOrigin: "0 0",
        width: 32,
        height: 17.25,
      }}>
        <path d={"M 0.366 0.366 C 0.854 -0.122 1.646 -0.122 2.134 0.366 L 16 14.232 L 29.866 0.366 C 30.354 -0.122 31.146 -0.122 31.634 0.366 C 32.122 0.854 32.122 1.646 31.634 2.134 L 16.884 16.884 C 16.396 17.372 15.604 17.372 15.116 16.884 L 0.366 2.134 C -0.122 1.646 -0.122 0.854 0.366 0.366 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={32} height={17.500} viewBox="0 0 32 17.500" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,32.500,7.750)",
        transformOrigin: "0 0",
        width: 32,
        height: 17.5,
      }}>
        <path d={"M 0.439 0.439 C 1.025 -0.146 1.975 -0.146 2.561 0.439 L 16 13.879 L 29.439 0.439 C 30.025 -0.146 30.975 -0.146 31.561 0.439 C 32.146 1.025 32.146 1.975 31.561 2.561 L 17.061 17.061 C 16.475 17.646 15.525 17.646 14.939 17.061 L 0.439 2.561 C -0.146 1.975 -0.146 1.025 0.439 0.439 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.250} height={19} viewBox="0 0 10.250 19" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 4,
        width: 10.25,
        height: 19,
      }}>
        <path d={"M 10.03 18.78 C 9.737 19.073 9.263 19.073 8.97 18.78 L 0.22 10.03 C 0.079 9.89 0 9.699 0 9.5 C 0 9.301 0.079 9.11 0.22 8.97 L 8.97 0.22 C 9.263 -0.073 9.737 -0.073 10.03 0.22 C 10.323 0.513 10.323 0.987 10.03 1.28 L 1.811 9.5 L 10.03 17.72 C 10.323 18.013 10.323 18.487 10.03 18.78 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10.750} height={19.500} viewBox="0 0 10.750 19.500" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 4,
        width: 10.75,
        height: 19.5,
      }}>
        <path d={"M 10.457 19.207 C 10.067 19.598 9.433 19.598 9.043 19.207 L 0.293 10.457 C 0.105 10.27 0 10.015 0 9.75 C 0 9.485 0.105 9.23 0.293 9.043 L 9.043 0.293 C 9.433 -0.098 10.067 -0.098 10.457 0.293 C 10.848 0.683 10.848 1.317 10.457 1.707 L 2.414 9.75 L 10.457 17.793 C 10.848 18.183 10.848 18.817 10.457 19.207 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8.750} height={16} viewBox="0 0 8.750 16" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 4,
        width: 8.75,
        height: 16,
      }}>
        <path d={"M 8.53 0.22 C 8.823 0.513 8.823 0.987 8.53 1.28 L 1.811 8 L 8.53 14.72 C 8.823 15.013 8.823 15.487 8.53 15.78 C 8.237 16.073 7.763 16.073 7.47 15.78 L 0.22 8.53 C -0.073 8.237 -0.073 7.763 0.22 7.47 L 7.47 0.22 C 7.763 -0.073 8.237 -0.073 8.53 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={9} height={16} viewBox="0 0 9 16" fill="none" style={{
        position: "absolute",
        left: 7,
        top: 4,
        width: 9,
        height: 16,
      }}>
        <path d={"M 8.707 0.293 C 9.098 0.683 9.098 1.317 8.707 1.707 L 2.414 8 L 8.707 14.293 C 9.098 14.683 9.098 15.317 8.707 15.707 C 8.317 16.098 7.683 16.098 7.293 15.707 L 0.293 8.707 C -0.098 8.317 -0.098 7.683 0.293 7.293 L 7.293 0.293 C 7.683 -0.098 8.317 -0.098 8.707 0.293 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.499} height={12.001} viewBox="0 0 6.499 12.001" fill="none" style={{
        position: "absolute",
        left: 6.001,
        top: 3.999,
        width: 6.499,
        height: 12.001,
      }}>
        <path d={"M 6.353 11.854 C 6.158 12.049 5.842 12.05 5.646 11.855 L 0.162 6.39 C -0.054 6.175 -0.054 5.826 0.162 5.611 L 5.646 0.146 C 5.842 -0.049 6.158 -0.049 6.353 0.147 C 6.548 0.343 6.548 0.659 6.352 0.854 L 1.188 6 L 6.352 11.147 C 6.548 11.342 6.548 11.658 6.353 11.854 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={6.501} height={12.001} viewBox="0 0 6.501 12.001" fill="none" style={{
        position: "absolute",
        left: 5.999,
        top: 3.999,
        width: 6.501,
        height: 12.001,
      }}>
        <path d={"M 6.269 11.794 C 5.969 12.08 5.494 12.068 5.208 11.768 L 0.207 6.518 C -0.069 6.228 -0.069 5.773 0.207 5.483 L 5.208 0.233 C 5.494 -0.067 5.969 -0.079 6.269 0.207 C 6.569 0.493 6.58 0.967 6.294 1.267 L 1.786 6 L 6.294 10.734 C 6.58 11.034 6.569 11.508 6.269 11.794 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={5.500} height={10} viewBox="0 0 5.500 10" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 3,
        width: 5.5,
        height: 10,
      }}>
        <path d={"M 5.354 0.146 C 5.549 0.342 5.549 0.658 5.354 0.854 L 1.207 5 L 5.354 9.146 C 5.549 9.342 5.549 9.658 5.354 9.854 C 5.158 10.049 4.842 10.049 4.646 9.854 L 0.146 5.354 C -0.049 5.158 -0.049 4.842 0.146 4.646 L 4.646 0.146 C 4.842 -0.049 5.158 -0.049 5.354 0.146 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={5.500} height={10} viewBox="0 0 5.500 10" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 3,
        width: 5.5,
        height: 10,
      }}>
        <path d={"M 5.26 0.2 C 5.564 0.482 5.581 0.957 5.3 1.26 L 1.773 5 L 5.3 8.74 C 5.581 9.043 5.564 9.518 5.26 9.8 C 4.957 10.081 4.482 10.064 4.2 9.76 L 0.2 5.51 C -0.067 5.223 -0.067 4.777 0.2 4.49 L 4.2 0.24 C 4.482 -0.064 4.957 -0.081 5.26 0.2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={4.500} height={8} viewBox="0 0 4.500 8" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 2,
        width: 4.5,
        height: 8,
      }}>
        <path d={"M 4.354 0.146 C 4.549 0.342 4.549 0.658 4.354 0.854 L 1.207 4 L 4.354 7.146 C 4.549 7.342 4.549 7.658 4.354 7.854 C 4.158 8.049 3.842 8.049 3.646 7.854 L 0.146 4.354 C -0.049 4.158 -0.049 3.842 0.146 3.646 L 3.646 0.146 C 3.842 -0.049 4.158 -0.049 4.354 0.146 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={4.750} height={8} viewBox="0 0 4.750 8" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 2,
        width: 4.75,
        height: 8,
      }}>
        <path d={"M 4.53 0.22 C 4.823 0.513 4.823 0.987 4.53 1.28 L 1.811 4 L 4.53 6.72 C 4.823 7.013 4.823 7.487 4.53 7.78 C 4.237 8.073 3.763 8.073 3.47 7.78 L 0.22 4.53 C -0.073 4.237 -0.073 3.763 0.22 3.47 L 3.47 0.22 C 3.763 -0.073 4.237 -0.073 4.53 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={32} height={17.250} viewBox="0 0 32 17.250" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 15.75,
        width: 32,
        height: 17.25,
      }}>
        <path d={"M 0.366 0.366 C -0.122 0.854 -0.122 1.646 0.366 2.134 L 15.116 16.884 C 15.604 17.372 16.396 17.372 16.884 16.884 L 31.634 2.134 C 32.122 1.646 32.122 0.854 31.634 0.366 C 31.146 -0.122 30.354 -0.122 29.866 0.366 L 16 14.232 L 2.134 0.366 C 1.646 -0.122 0.854 -0.122 0.366 0.366 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={32} height={17.500} viewBox="0 0 32 17.500" fill="none" style={{
        position: "absolute",
        left: 8,
        top: 15.5,
        width: 32,
        height: 17.5,
      }}>
        <path d={"M 0.439 0.439 C 1.025 -0.146 1.975 -0.146 2.561 0.439 L 16 13.879 L 29.439 0.439 C 30.025 -0.146 30.975 -0.146 31.561 0.439 C 32.146 1.025 32.146 1.975 31.561 2.561 L 17.061 17.061 C 16.475 17.646 15.525 17.646 14.939 17.061 L 0.439 2.561 C -0.146 1.975 -0.146 1.025 0.439 0.439 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={10.750} viewBox="0 0 20 10.750" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 9.25,
        width: 20,
        height: 10.75,
      }}>
        <path d={"M 0.22 0.22 C 0.513 -0.073 0.987 -0.073 1.28 0.22 L 10 8.939 L 18.72 0.22 C 19.013 -0.073 19.487 -0.073 19.78 0.22 C 20.073 0.513 20.073 0.987 19.78 1.28 L 10.53 10.53 C 10.237 10.823 9.763 10.823 9.47 10.53 L 0.22 1.28 C -0.073 0.987 -0.073 0.513 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={20} height={11} viewBox="0 0 20 11" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 9,
        width: 20,
        height: 11,
      }}>
        <path d={"M 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 10 8.586 L 18.293 0.293 C 18.683 -0.098 19.317 -0.098 19.707 0.293 C 20.098 0.683 20.098 1.317 19.707 1.707 L 10.707 10.707 C 10.317 11.098 9.683 11.098 9.293 10.707 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={8.750} viewBox="0 0 16 8.750" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 8.25,
        width: 16,
        height: 8.75,
      }}>
        <path d={"M 0.22 0.22 C 0.513 -0.073 0.987 -0.073 1.28 0.22 L 8 6.939 L 14.72 0.22 C 15.013 -0.073 15.487 -0.073 15.78 0.22 C 16.073 0.513 16.073 0.987 15.78 1.28 L 8.53 8.53 C 8.237 8.823 7.763 8.823 7.47 8.53 L 0.22 1.28 C -0.073 0.987 -0.073 0.513 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={16} height={9} viewBox="0 0 16 9" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 8,
        width: 16,
        height: 9,
      }}>
        <path d={"M 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 8 6.586 L 14.293 0.293 C 14.683 -0.098 15.317 -0.098 15.707 0.293 C 16.098 0.683 16.098 1.317 15.707 1.707 L 8.707 8.707 C 8.317 9.098 7.683 9.098 7.293 8.707 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12.001} height={6.499} viewBox="0 0 12.001 6.499" fill="none" style={{
        position: "absolute",
        left: 3.999,
        top: 7.5,
        width: 12.001,
        height: 6.499,
      }}>
        <path d={"M 11.854 0.146 C 12.049 0.341 12.05 0.657 11.855 0.853 L 6.39 6.337 C 6.175 6.553 5.826 6.553 5.611 6.337 L 0.146 0.853 C -0.049 0.657 -0.049 0.341 0.147 0.146 C 0.343 -0.049 0.659 -0.049 0.854 0.147 L 6 5.312 L 11.147 0.147 C 11.342 -0.049 11.658 -0.049 11.854 0.146 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12.001} height={6.501} viewBox="0 0 12.001 6.501" fill="none" style={{
        position: "absolute",
        left: 3.999,
        top: 7.5,
        width: 12.001,
        height: 6.501,
      }}>
        <path d={"M 11.794 0.233 C 12.08 0.533 12.068 1.007 11.768 1.293 L 6.518 6.294 C 6.228 6.57 5.773 6.57 5.483 6.294 L 0.233 1.293 C -0.067 1.007 -0.079 0.533 0.207 0.233 C 0.493 -0.067 0.967 -0.079 1.267 0.207 L 6 4.716 L 10.734 0.207 C 11.034 -0.079 11.508 -0.067 11.794 0.233 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={5.500} viewBox="0 0 10 5.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 5.5,
        width: 10,
        height: 5.5,
      }}>
        <path d={"M 0.146 0.146 C 0.342 -0.049 0.658 -0.049 0.854 0.146 L 5 4.293 L 9.146 0.146 C 9.342 -0.049 9.658 -0.049 9.854 0.146 C 10.049 0.342 10.049 0.658 9.854 0.854 L 5.354 5.354 C 5.158 5.549 4.842 5.549 4.646 5.354 L 0.146 0.854 C -0.049 0.658 -0.049 0.342 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={5.500} viewBox="0 0 10 5.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 5.5,
        width: 10,
        height: 5.5,
      }}>
        <path d={"M 0.2 0.24 C 0.482 -0.064 0.957 -0.081 1.26 0.2 L 5 3.727 L 8.74 0.2 C 9.043 -0.081 9.518 -0.064 9.8 0.24 C 10.081 0.543 10.064 1.018 9.76 1.3 L 5.51 5.3 C 5.223 5.567 4.777 5.567 4.49 5.3 L 0.24 1.3 C -0.064 1.018 -0.081 0.543 0.2 0.24 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8} height={4.500} viewBox="0 0 8 4.500" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 4.5,
        width: 8,
        height: 4.5,
      }}>
        <path d={"M 0.146 0.146 C 0.342 -0.049 0.658 -0.049 0.854 0.146 L 4 3.293 L 7.146 0.146 C 7.342 -0.049 7.658 -0.049 7.854 0.146 C 8.049 0.342 8.049 0.658 7.854 0.854 L 4.354 4.354 C 4.158 4.549 3.842 4.549 3.646 4.354 L 0.146 0.854 C -0.049 0.658 -0.049 0.342 0.146 0.146 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={8} height={4.750} viewBox="0 0 8 4.750" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 4.25,
        width: 8,
        height: 4.75,
      }}>
        <path d={"M 0.22 0.22 C 0.513 -0.073 0.987 -0.073 1.28 0.22 L 4 2.939 L 6.72 0.22 C 7.013 -0.073 7.487 -0.073 7.78 0.22 C 8.073 0.513 8.073 0.987 7.78 1.28 L 4.53 4.53 C 4.237 4.823 3.763 4.823 3.47 4.53 L 0.22 1.28 C -0.073 0.987 -0.073 0.513 0.22 0.22 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={22} height={12} viewBox="0 0 22 12" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 11,
        width: 22,
        height: 12,
      }}>
        <path d={"M 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 11 9.586 L 20.293 0.293 C 20.683 -0.098 21.317 -0.098 21.707 0.293 C 22.098 0.683 22.098 1.317 21.707 1.707 L 11.707 11.707 C 11.317 12.098 10.683 12.098 10.293 11.707 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={22} height={12.250} viewBox="0 0 22 12.250" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 10.75,
        width: 22,
        height: 12.25,
      }}>
        <path d={"M 0.366 0.366 C 0.854 -0.122 1.646 -0.122 2.134 0.366 L 11 9.232 L 19.866 0.366 C 20.354 -0.122 21.146 -0.122 21.634 0.366 C 22.122 0.854 22.122 1.646 21.634 2.134 L 11.884 11.884 C 11.396 12.372 10.604 12.372 10.116 11.884 L 0.366 2.134 C -0.122 1.646 -0.122 0.854 0.366 0.366 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={22} height={12} viewBox="0 0 22 12" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 9,
        width: 22,
        height: 12,
      }}>
        <path d={"M 0.293 11.707 C 0.683 12.098 1.317 12.098 1.707 11.707 L 11 2.414 L 20.293 11.707 C 20.683 12.098 21.317 12.098 21.707 11.707 C 22.098 11.317 22.098 10.683 21.707 10.293 L 11.707 0.293 C 11.317 -0.098 10.683 -0.098 10.293 0.293 L 0.293 10.293 C -0.098 10.683 -0.098 11.317 0.293 11.707 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={22} height={12.250} viewBox="0 0 22 12.250" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 9,
        width: 22,
        height: 12.25,
      }}>
        <path d={"M 0.366 11.884 C 0.854 12.372 1.646 12.372 2.134 11.884 L 11 3.018 L 19.866 11.884 C 20.354 12.372 21.146 12.372 21.634 11.884 C 22.122 11.396 22.122 10.604 21.634 10.116 L 11.884 0.366 C 11.396 -0.122 10.604 -0.122 10.116 0.366 L 0.366 10.116 C -0.122 10.604 -0.122 11.396 0.366 11.884 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={22} viewBox="0 0 12 22" fill="none" style={{
        position: "absolute",
        left: 9,
        top: 5,
        width: 12,
        height: 22,
      }}>
        <path d={"M 11.707 0.293 C 12.098 0.683 12.098 1.317 11.707 1.707 L 2.414 11 L 11.707 20.293 C 12.098 20.683 12.098 21.317 11.707 21.707 C 11.317 22.098 10.683 22.098 10.293 21.707 L 0.293 11.707 C -0.098 11.317 -0.098 10.683 0.293 10.293 L 10.293 0.293 C 10.683 -0.098 11.317 -0.098 11.707 0.293 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12.250} height={22} viewBox="0 0 12.250 22" fill="none" style={{
        position: "absolute",
        left: 9,
        top: 5,
        width: 12.25,
        height: 22,
      }}>
        <path d={"M 11.884 0.366 C 12.372 0.854 12.372 1.646 11.884 2.134 L 3.018 11 L 11.884 19.866 C 12.372 20.354 12.372 21.146 11.884 21.634 C 11.396 22.122 10.604 22.122 10.116 21.634 L 0.366 11.884 C -0.122 11.396 -0.122 10.604 0.366 10.116 L 10.116 0.366 C 10.604 -0.122 11.396 -0.122 11.884 0.366 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={22} viewBox="0 0 12 22" fill="none" style={{
        position: "absolute",
        left: 11,
        top: 5,
        width: 12,
        height: 22,
      }}>
        <path d={"M 0.293 21.707 C -0.098 21.317 -0.098 20.683 0.293 20.293 L 9.586 11 L 0.293 1.707 C -0.098 1.317 -0.098 0.683 0.293 0.293 C 0.683 -0.098 1.317 -0.098 1.707 0.293 L 11.707 10.293 C 12.098 10.683 12.098 11.317 11.707 11.707 L 1.707 21.707 C 1.317 22.098 0.683 22.098 0.293 21.707 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12.250} height={22} viewBox="0 0 12.250 22" fill="none" style={{
        position: "absolute",
        left: 10.75,
        top: 5,
        width: 12.25,
        height: 22,
      }}>
        <path d={"M 0.366 21.634 C -0.122 21.146 -0.122 20.354 0.366 19.866 L 9.232 11 L 0.366 2.134 C -0.122 1.646 -0.122 0.854 0.366 0.366 C 0.854 -0.122 1.646 -0.122 2.134 0.366 L 11.884 10.116 C 12.372 10.604 12.372 11.396 11.884 11.884 L 2.134 21.634 C 1.646 22.122 0.854 22.122 0.366 21.634 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Direction=Up, Size=48, Theme=Regular
    "direction=up|size=48|theme=regular": __body0,
    // figma: Direction=Up, Size=48, Theme=Filled
    "direction=up|size=48|theme=filled": __body1,
    // figma: Direction=Up, Size=28, Theme=Regular
    "direction=up|size=28|theme=regular": __body2,
    // figma: Direction=Up, Size=28, Theme=Filled
    "direction=up|size=28|theme=filled": __body3,
    // figma: Direction=Up, Size=24, Theme=Regular
    "direction=up|size=24|theme=regular": __body4,
    // figma: Direction=Up, Size=24, Theme=Filled
    "direction=up|size=24|theme=filled": __body5,
    // figma: Direction=Up, Size=20, Theme=Regular
    "direction=up|size=20|theme=regular": __body6,
    // figma: Direction=Up, Size=20, Theme=Filled
    "direction=up|size=20|theme=filled": __body7,
    // figma: Direction=Up, Size=16, Theme=Regular
    "direction=up|size=16|theme=regular": __body8,
    // figma: Direction=Up, Size=16, Theme=Filled
    "direction=up|size=16|theme=filled": __body9,
    // figma: Direction=Up, Size=12, Theme=Regular
    "direction=up|size=12|theme=regular": __body10,
    // figma: Direction=Up, Size=12, Theme=Filled
    "direction=up|size=12|theme=filled": __body11,
    // figma: Direction=Right, Size=48, Theme=Regular
    "direction=right|size=48|theme=regular": __body12,
    // figma: Direction=Right, Size=48, Theme=Filled
    "direction=right|size=48|theme=filled": __body13,
    // figma: Direction=Right, Size=28, Theme=Regular
    "direction=right|size=28|theme=regular": __body14,
    // figma: Direction=Right, Size=28, Theme=Filled
    "direction=right|size=28|theme=filled": __body15,
    // figma: Direction=Right, Size=24, Theme=Regular
    "direction=right|size=24|theme=regular": __body16,
    // figma: Direction=Right, Size=24, Theme=Filled
    "direction=right|size=24|theme=filled": __body17,
    // figma: Direction=Right, Size=20, Theme=Regular
    "direction=right|size=20|theme=regular": __body18,
    // figma: Direction=Right, Size=20, Theme=Filled
    "direction=right|size=20|theme=filled": __body19,
    // figma: Direction=Right, Size=16, Theme=Regular
    "direction=right|size=16|theme=regular": __body20,
    // figma: Direction=Right, Size=16, Theme=Filled
    "direction=right|size=16|theme=filled": __body21,
    // figma: Direction=Right, Size=12, Theme=Regular
    "direction=right|size=12|theme=regular": __body22,
    // figma: Direction=Right, Size=12, Theme=Filled
    "direction=right|size=12|theme=filled": __body23,
    // figma: Direction=Left, Size=48, Theme=Regular
    "direction=left|size=48|theme=regular": __body24,
    // figma: Direction=Left, Size=48, Theme=Filled
    "direction=left|size=48|theme=filled": __body25,
    // figma: Direction=Left, Size=28, Theme=Regular
    "direction=left|size=28|theme=regular": __body26,
    // figma: Direction=Left, Size=28, Theme=Filled
    "direction=left|size=28|theme=filled": __body27,
    // figma: Direction=Left, Size=24, Theme=Regular
    "direction=left|size=24|theme=regular": __body28,
    // figma: Direction=Left, Size=24, Theme=Filled
    "direction=left|size=24|theme=filled": __body29,
    // figma: Direction=Left, Size=20, Theme=Regular
    "direction=left|size=20|theme=regular": __body30,
    // figma: Direction=Left, Size=20, Theme=Filled
    "direction=left|size=20|theme=filled": __body31,
    // figma: Direction=Left, Size=16, Theme=Regular
    "direction=left|size=16|theme=regular": __body32,
    // figma: Direction=Left, Size=16, Theme=Filled
    "direction=left|size=16|theme=filled": __body33,
    // figma: Direction=Left, Size=12, Theme=Regular
    "direction=left|size=12|theme=regular": __body34,
    // figma: Direction=Left, Size=12, Theme=Filled
    "direction=left|size=12|theme=filled": __body35,
    // figma: Direction=Down, Size=48, Theme=Regular
    "direction=down|size=48|theme=regular": __body36,
    // figma: Direction=Down, Size=48, Theme=Filled
    "direction=down|size=48|theme=filled": __body37,
    // figma: Direction=Down, Size=28, Theme=Regular
    "direction=down|size=28|theme=regular": __body38,
    // figma: Direction=Down, Size=28, Theme=Filled
    "direction=down|size=28|theme=filled": __body39,
    // figma: Direction=Down, Size=24, Theme=Regular
    "direction=down|size=24|theme=regular": __body40,
    // figma: Direction=Down, Size=24, Theme=Filled
    "direction=down|size=24|theme=filled": __body41,
    // figma: Direction=Down, Size=20, Theme=Regular
    "direction=down|size=20|theme=regular": __body42,
    // figma: Direction=Down, Size=20, Theme=Filled
    "direction=down|size=20|theme=filled": __body43,
    // figma: Direction=Down, Size=16, Theme=Regular
    "direction=down|size=16|theme=regular": __body44,
    // figma: Direction=Down, Size=16, Theme=Filled
    "direction=down|size=16|theme=filled": __body45,
    // figma: Direction=Down, Size=12, Theme=Regular
    "direction=down|size=12|theme=regular": __body46,
    // figma: Direction=Down, Size=12, Theme=Filled
    "direction=down|size=12|theme=filled": __body47,
    // figma: Direction=Down, Size=32, Theme=Regular
    "direction=down|size=32|theme=regular": __body48,
    // figma: Direction=Down, Size=32, Theme=Filled
    "direction=down|size=32|theme=filled": __body49,
    // figma: Direction=Up, Size=32, Theme=Regular
    "direction=up|size=32|theme=regular": __body50,
    // figma: Direction=Up, Size=32, Theme=Filled
    "direction=up|size=32|theme=filled": __body51,
    // figma: Direction=Left, Size=32, Theme=Regular
    "direction=left|size=32|theme=regular": __body52,
    // figma: Direction=Left, Size=32, Theme=Filled
    "direction=left|size=32|theme=filled": __body53,
    // figma: Direction=Right, Size=32, Theme=Regular
    "direction=right|size=32|theme=regular": __body54,
    // figma: Direction=Right, Size=32, Theme=Filled
    "direction=right|size=32|theme=filled": __body55,
  };
  return (__impls[__vkey(props)] ?? __body46)();
}
export default Chevron3;
