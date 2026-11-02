import { __slateNorm } from './slateProps.jsx';
// figma node: 9064:10210 Warning (14 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Warning(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","28","32","48"],"theme":["regular","filled"]});
  const props = { ..._p, size: _p.size ?? "12", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={19.997} height={18.501} viewBox="0 0 19.997 18.501" fill="none" style={{
        position: "absolute",
        left: 2.002,
        top: 2.498,
        width: 19.997,
        height: 18.501,
      }}>
        <path d={"M 10.997 14.502 C 10.997 13.95 10.55 13.503 9.999 13.503 C 9.447 13.503 9 13.95 9 14.502 C 9 15.053 9.447 15.501 9.999 15.501 C 10.55 15.501 10.997 15.053 10.997 14.502 Z M 10.738 6.649 C 10.688 6.283 10.374 6.001 9.994 6.001 C 9.58 6.001 9.245 6.337 9.245 6.752 L 9.249 11.253 L 9.256 11.355 C 9.306 11.721 9.62 12.003 9.999 12.003 C 10.413 12.002 10.749 11.666 10.749 11.252 L 10.745 6.75 L 10.738 6.649 Z M 11.967 1.161 C 11.111 -0.387 8.886 -0.387 8.03 1.161 L 0.284 15.162 C -0.545 16.662 0.539 18.501 2.253 18.501 L 17.744 18.501 C 19.458 18.501 20.543 16.662 19.713 15.162 L 11.967 1.161 Z M 9.342 1.887 C 9.628 1.371 10.369 1.371 10.655 1.887 L 18.401 15.888 C 18.677 16.388 18.316 17.001 17.744 17.001 L 2.253 17.001 C 1.682 17.001 1.32 16.388 1.597 15.888 L 9.342 1.887 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={19.997} height={18.501} viewBox="0 0 19.997 18.501" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2.498,
        width: 19.997,
        height: 18.501,
      }}>
        <path d={"M 8.03 1.161 C 8.886 -0.387 11.111 -0.387 11.967 1.161 L 19.713 15.162 C 20.543 16.662 19.458 18.501 17.744 18.501 L 2.253 18.501 C 0.539 18.501 -0.545 16.662 0.284 15.162 L 8.03 1.161 Z M 10.997 14.503 C 10.997 13.951 10.55 13.504 9.999 13.504 C 9.447 13.504 9 13.951 9 14.503 C 9 15.054 9.447 15.502 9.999 15.502 C 10.55 15.502 10.997 15.054 10.997 14.503 Z M 10.738 6.65 C 10.688 6.284 10.374 6.002 9.994 6.002 C 9.58 6.002 9.245 6.338 9.245 6.753 L 9.249 11.254 L 9.256 11.356 C 9.306 11.722 9.62 12.004 9.999 12.004 C 10.413 12.003 10.749 11.667 10.749 11.253 L 10.745 6.751 L 10.738 6.65 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16.007} height={15.001} viewBox="0 0 16.007 15.001" fill="none" style={{
        position: "absolute",
        left: 1.997,
        top: 2,
        width: 16.007,
        height: 15.001,
      }}>
        <path d={"M 7.565 1.262 C 7.754 0.913 8.255 0.913 8.444 1.262 L 14.944 13.263 C 15.125 13.596 14.884 14.001 14.505 14.001 L 1.502 14.001 C 1.123 14.001 0.882 13.596 1.062 13.263 L 7.565 1.262 Z M 9.323 0.786 C 8.756 -0.262 7.253 -0.262 6.685 0.785 L 0.183 12.786 C -0.358 13.786 0.365 15.001 1.502 15.001 L 14.505 15.001 C 15.641 15.001 16.365 13.786 15.824 12.787 L 9.323 0.786 Z M 8.503 5.5 C 8.503 5.224 8.279 5 8.003 5 C 7.727 5 7.503 5.224 7.503 5.5 L 7.503 9.5 C 7.503 9.776 7.727 10 8.003 10 C 8.279 10 8.503 9.776 8.503 9.5 L 8.503 5.5 Z M 8.753 11.75 C 8.753 12.164 8.417 12.5 8.003 12.5 C 7.588 12.5 7.253 12.164 7.253 11.75 C 7.253 11.336 7.588 11 8.003 11 C 8.417 11 8.753 11.336 8.753 11.75 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16.007} height={15.001} viewBox="0 0 16.007 15.001" fill="none" style={{
        position: "absolute",
        left: 1.996,
        top: 2,
        width: 16.007,
        height: 15.001,
      }}>
        <path d={"M 6.685 0.785 C 7.253 -0.262 8.756 -0.262 9.323 0.786 L 15.824 12.787 C 16.365 13.786 15.641 15.001 14.505 15.001 L 1.502 15.001 C 0.365 15.001 -0.358 13.786 0.183 12.786 L 6.685 0.785 Z M 8.504 5.5 C 8.504 5.224 8.28 5 8.004 5 C 7.728 5 7.504 5.224 7.504 5.5 L 7.504 9.5 C 7.504 9.776 7.728 10 8.004 10 C 8.28 10 8.504 9.776 8.504 9.5 L 8.504 5.5 Z M 8.754 11.75 C 8.754 11.336 8.418 11 8.004 11 C 7.59 11 7.254 11.336 7.254 11.75 C 7.254 12.164 7.59 12.5 8.004 12.5 C 8.418 12.5 8.754 12.164 8.754 11.75 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.007} height={11.002} viewBox="0 0 12.007 11.002" fill="none" style={{
        position: "absolute",
        left: 1.995,
        top: 1.997,
        width: 12.007,
        height: 11.002,
      }}>
        <path d={"M 6.755 8.253 C 6.755 8.667 6.419 9.003 6.005 9.003 C 5.591 9.003 5.255 8.667 5.255 8.253 C 5.255 7.838 5.591 7.503 6.005 7.503 C 6.419 7.503 6.755 7.838 6.755 8.253 Z M 5.505 6.003 C 5.505 6.279 5.729 6.503 6.005 6.503 C 6.281 6.503 6.505 6.279 6.505 6.003 L 6.505 3.503 C 6.505 3.226 6.281 3.003 6.005 3.003 C 5.729 3.003 5.505 3.226 5.505 3.503 L 5.505 6.003 Z M 4.913 0.64 C 5.391 -0.213 6.619 -0.213 7.096 0.64 L 11.846 9.142 C 12.312 9.975 11.71 11.002 10.755 11.002 L 1.252 11.002 C 0.297 11.002 -0.305 9.975 0.161 9.142 L 4.913 0.64 Z M 6.223 1.128 C 6.127 0.957 5.882 0.957 5.786 1.128 L 1.034 9.63 C 0.94 9.796 1.061 10.002 1.252 10.002 L 10.755 10.002 C 10.946 10.002 11.066 9.796 10.973 9.63 L 6.223 1.128 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.007} height={11.002} viewBox="0 0 12.007 11.002" fill="none" style={{
        position: "absolute",
        left: 1.996,
        top: 1.998,
        width: 12.007,
        height: 11.002,
      }}>
        <path d={"M 7.096 0.64 C 6.619 -0.213 5.391 -0.213 4.913 0.64 L 0.161 9.142 C -0.305 9.975 0.297 11.002 1.252 11.002 L 10.755 11.002 C 11.71 11.002 12.312 9.975 11.846 9.142 L 7.096 0.64 Z M 6.754 8.252 C 6.754 8.666 6.418 9.002 6.004 9.002 C 5.59 9.002 5.254 8.666 5.254 8.252 C 5.254 7.838 5.59 7.502 6.004 7.502 C 6.418 7.502 6.754 7.838 6.754 8.252 Z M 5.504 6.002 L 5.504 3.502 C 5.504 3.226 5.728 3.002 6.004 3.002 C 6.28 3.002 6.504 3.226 6.504 3.502 L 6.504 6.002 C 6.504 6.278 6.28 6.502 6.004 6.502 C 5.728 6.502 5.504 6.278 5.504 6.002 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={9} viewBox="0 0 10 9" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 10,
        height: 9,
      }}>
        <path d={"M 4.5 3.5 C 4.5 3.224 4.724 3 5 3 C 5.276 3 5.5 3.224 5.5 3.5 L 5.5 4.5 C 5.5 4.776 5.276 5 5 5 C 4.724 5 4.5 4.776 4.5 4.5 L 4.5 3.5 Z M 5 5.75 C 4.586 5.75 4.25 6.086 4.25 6.5 C 4.25 6.914 4.586 7.25 5 7.25 C 5.414 7.25 5.75 6.914 5.75 6.5 C 5.75 6.086 5.414 5.75 5 5.75 Z M 4.214 0.459 C 4.563 -0.153 5.437 -0.153 5.786 0.459 L 9.878 7.628 C 10.226 8.238 9.789 9 9.091 9 L 0.909 9 C 0.211 9 -0.226 8.238 0.122 7.628 L 4.214 0.459 Z M 5 1.119 L 1.083 7.983 L 8.917 7.983 L 5 1.119 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={9} viewBox="0 0 10 9" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 10,
        height: 9,
      }}>
        <path d={"M 4.214 0.459 C 4.563 -0.153 5.437 -0.153 5.786 0.459 L 9.878 7.628 C 10.226 8.238 9.789 9 9.091 9 L 0.909 9 C 0.211 9 -0.226 8.238 0.122 7.628 L 4.214 0.459 Z M 4.5 3.5 L 4.5 4.5 C 4.5 4.776 4.724 5 5 5 C 5.276 5 5.5 4.776 5.5 4.5 L 5.5 3.5 C 5.5 3.224 5.276 3 5 3 C 4.724 3 4.5 3.224 4.5 3.5 Z M 5 5.75 C 4.586 5.75 4.25 6.086 4.25 6.5 C 4.25 6.914 4.586 7.25 5 7.25 C 5.414 7.25 5.75 6.914 5.75 6.5 C 5.75 6.086 5.414 5.75 5 5.75 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={23.991} height={22.249} viewBox="0 0 23.991 22.249" fill="none" style={{
        position: "absolute",
        left: 2.004,
        top: 2.75,
        width: 23.991,
        height: 22.249,
      }}>
        <path d={"M 14.404 1.422 C 13.359 -0.474 10.635 -0.474 9.589 1.421 L 0.346 18.171 C -0.665 20.004 0.661 22.249 2.754 22.249 L 21.237 22.249 C 23.331 22.249 24.656 20.004 23.645 18.171 L 14.404 1.422 Z M 12.996 17.25 C 12.996 17.802 12.548 18.25 11.996 18.25 C 11.444 18.25 10.996 17.802 10.996 17.25 C 10.996 16.698 11.444 16.25 11.996 16.25 C 12.548 16.25 12.996 16.698 12.996 17.25 Z M 11.246 14 L 11.246 7.5 C 11.246 7.086 11.582 6.75 11.996 6.75 C 12.41 6.75 12.746 7.086 12.746 7.5 L 12.746 14 C 12.746 14.414 12.41 14.75 11.996 14.75 C 11.582 14.75 11.246 14.414 11.246 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24.002} height={22.251} viewBox="0 0 24.002 22.251" fill="none" style={{
        position: "absolute",
        left: 2.003,
        top: 2.749,
        width: 24.002,
        height: 22.251,
      }}>
        <path d={"M 12.997 17.251 C 12.997 17.803 12.549 18.251 11.997 18.251 C 11.445 18.251 10.997 17.803 10.997 17.251 C 10.997 16.699 11.445 16.251 11.997 16.251 C 12.549 16.251 12.997 16.699 12.997 17.251 Z M 11.247 14.001 C 11.247 14.415 11.583 14.751 11.997 14.751 C 12.411 14.751 12.747 14.415 12.747 14.001 L 12.747 7.501 C 12.747 7.087 12.411 6.751 11.997 6.751 C 11.583 6.751 11.247 7.087 11.247 7.501 L 11.247 14.001 Z M 9.59 1.421 C 10.635 -0.473 13.358 -0.474 14.405 1.421 L 23.655 18.171 C 24.667 20.004 23.341 22.251 21.248 22.251 L 2.754 22.251 C 0.661 22.251 -0.665 20.005 0.346 18.172 L 9.59 1.421 Z M 13.092 2.146 C 12.616 1.285 11.378 1.285 10.903 2.146 L 1.659 18.897 C 1.2 19.73 1.802 20.751 2.754 20.751 L 21.248 20.751 C 22.199 20.751 22.802 19.73 22.342 18.897 L 13.092 2.146 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={27.997} height={25.999} viewBox="0 0 27.997 25.999" fill="none" style={{
        position: "absolute",
        left: 2.002,
        top: 2.001,
        width: 27.997,
        height: 25.999,
      }}>
        <path d={"M 15.248 19.999 C 15.248 20.689 14.688 21.249 13.998 21.249 C 13.307 21.249 12.748 20.689 12.748 19.999 C 12.748 19.309 13.307 18.749 13.998 18.749 C 14.688 18.749 15.248 19.309 15.248 19.999 Z M 13.998 6.999 C 13.446 6.999 12.998 7.447 12.998 7.999 L 12.998 15.999 C 12.998 16.551 13.446 16.999 13.998 16.999 C 14.55 16.999 14.998 16.551 14.998 15.999 L 14.998 7.999 C 14.998 7.447 14.55 6.999 13.998 6.999 Z M 10.934 1.808 C 12.266 -0.603 15.73 -0.603 17.062 1.808 L 27.555 20.807 C 28.844 23.14 27.156 25.999 24.492 25.999 L 3.505 25.999 C 0.84 25.999 -0.847 23.14 0.441 20.807 L 10.934 1.808 Z M 15.311 2.775 C 14.741 1.742 13.256 1.742 12.685 2.775 L 2.192 21.774 C 1.64 22.774 2.363 23.999 3.505 23.999 L 24.492 23.999 C 25.634 23.999 26.357 22.774 25.805 21.774 L 15.311 2.775 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={27.997} height={25.999} viewBox="0 0 27.997 25.999" fill="none" style={{
        position: "absolute",
        left: 2.001,
        top: 2.001,
        width: 27.997,
        height: 25.999,
      }}>
        <path d={"M 17.062 1.808 C 15.73 -0.603 12.266 -0.603 10.934 1.808 L 0.441 20.807 C -0.847 23.14 0.84 25.999 3.505 25.999 L 24.492 25.999 C 27.156 25.999 28.844 23.14 27.555 20.807 L 17.062 1.808 Z M 15.249 19.999 C 15.249 20.69 14.689 21.249 13.999 21.249 C 13.308 21.249 12.749 20.69 12.749 19.999 C 12.749 19.309 13.308 18.749 13.999 18.749 C 14.689 18.749 15.249 19.309 15.249 19.999 Z M 13.999 6.999 C 14.551 6.999 14.999 7.447 14.999 7.999 L 14.999 15.999 C 14.999 16.552 14.551 16.999 13.999 16.999 C 13.446 16.999 12.999 16.552 12.999 15.999 L 12.999 7.999 C 12.999 7.447 13.446 6.999 13.999 6.999 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={39.999} height={38.002} viewBox="0 0 39.999 38.002" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 39.999,
        height: 38.002,
      }}>
        <path d={"M 21.758 3.435 C 21.106 2.196 19.336 2.186 18.67 3.418 L 2.717 32.919 C 2.086 34.085 2.93 35.502 4.256 35.502 L 35.744 35.502 C 37.061 35.502 37.906 34.102 37.292 32.936 L 21.758 3.435 Z M 16.471 2.228 C 18.088 -0.762 22.387 -0.738 23.97 2.27 L 39.504 31.772 C 40.994 34.602 38.942 38.002 35.744 38.002 L 4.256 38.002 C 1.037 38.002 -1.014 34.562 0.518 31.73 L 16.471 2.228 Z M 20 11 C 20.69 11 21.25 11.56 21.25 12.25 L 21.25 23.75 C 21.25 24.44 20.69 25 20 25 C 19.31 25 18.75 24.44 18.75 23.75 L 18.75 12.25 C 18.75 11.56 19.31 11 20 11 Z M 20 31 C 21.105 31 22 30.105 22 29 C 22 27.895 21.105 27 20 27 C 18.895 27 18 27.895 18 29 C 18 30.105 18.895 31 20 31 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={39.999} height={38.002} viewBox="0 0 39.999 38.002" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 39.999,
        height: 38.002,
      }}>
        <path d={"M 16.471 2.228 C 18.088 -0.762 22.387 -0.738 23.97 2.27 L 39.504 31.772 C 40.994 34.602 38.942 38.002 35.744 38.002 L 4.256 38.002 C 1.037 38.002 -1.014 34.562 0.518 31.73 L 16.471 2.228 Z M 20 11 C 19.31 11 18.75 11.56 18.75 12.25 L 18.75 23.75 C 18.75 24.44 19.31 25 20 25 C 20.69 25 21.25 24.44 21.25 23.75 L 21.25 12.25 C 21.25 11.56 20.69 11 20 11 Z M 20 31 C 21.105 31 22 30.105 22 29 C 22 27.895 21.105 27 20 27 C 18.895 27 18 27.895 18 29 C 18 30.105 18.895 31 20 31 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body6,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body7,
    // figma: Size=28, Theme=Filled
    "size=28|theme=filled": __body8,
    // figma: Size=28, Theme=Regular
    "size=28|theme=regular": __body9,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body10,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body11,
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body12,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body13,
  };
  return (__impls[__vkey(props)] ?? __body6)();
}
export default Warning;
