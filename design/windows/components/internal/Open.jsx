import { __slateNorm } from './slateProps.jsx';
// figma node: 9064:11363 Open (14 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Open(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","28","32","48"],"theme":["regular","filled"]});
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
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 36,
        height: 36,
      }}>
        <path d={"M 6.25 2.5 C 4.179 2.5 2.5 4.179 2.5 6.25 L 2.5 29.75 C 2.5 31.821 4.179 33.5 6.25 33.5 L 29.75 33.5 C 31.821 33.5 33.5 31.821 33.5 29.75 L 33.5 21.25 C 33.5 20.56 34.06 20 34.75 20 C 35.44 20 36 20.56 36 21.25 L 36 29.75 C 36 33.202 33.202 36 29.75 36 L 6.25 36 C 2.798 36 0 33.202 0 29.75 L 0 6.25 C 0 2.798 2.798 0 6.25 0 L 14.75 0 C 15.44 0 16 0.56 16 1.25 C 16 1.94 15.44 2.5 14.75 2.5 L 6.25 2.5 Z M 21 1.25 C 21 0.56 21.56 0 22.25 0 L 34.75 0 C 35.44 0 36 0.56 36 1.25 L 36 13.75 C 36 14.44 35.44 15 34.75 15 C 34.06 15 33.5 14.44 33.5 13.75 L 33.5 4.268 L 23.134 14.634 C 22.646 15.122 21.854 15.122 21.366 14.634 C 20.878 14.146 20.878 13.354 21.366 12.866 L 31.732 2.5 L 22.25 2.5 C 21.56 2.5 21 1.94 21 1.25 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 35.451 0.34 C 35.786 0.615 36 1.032 36 1.5 L 36 13.5 C 36 14.328 35.328 15 34.5 15 C 33.672 15 33 14.328 33 13.5 L 33 5.121 L 23.56 14.561 C 22.975 15.146 22.025 15.146 21.439 14.561 C 20.853 13.975 20.853 13.025 21.439 12.439 L 30.878 3 L 22.5 3 C 21.672 3 21 2.329 21 1.5 C 21 0.672 21.672 0 22.5 0 L 34.481 0 C 34.547 -0.001 34.612 0.003 34.677 0.01 C 34.81 0.026 34.937 0.059 35.057 0.107 C 35.197 0.163 35.33 0.24 35.451 0.34 Z M 6.5 3 C 4.567 3 3 4.567 3 6.5 L 3 29.5 C 3 31.433 4.567 33 6.5 33 L 29.5 33 C 31.433 33 33 31.433 33 29.5 L 33 21.5 C 33 20.672 33.671 20 34.5 20 C 35.328 20 36 20.672 36 21.5 L 36 29.5 C 36 33.09 33.09 36 29.5 36 L 6.5 36 C 2.91 36 0 33.09 0 29.5 L 0 6.5 C 0 2.91 2.91 0 6.5 0 L 14.5 0 C 15.328 0 16 0.672 16 1.5 C 16 2.329 15.328 3 14.5 3 L 6.5 3 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 4.25 1.5 C 2.731 1.5 1.5 2.731 1.5 4.25 L 1.5 17.75 C 1.5 19.269 2.731 20.5 4.25 20.5 L 17.75 20.5 C 19.269 20.5 20.5 19.269 20.5 17.75 L 20.5 13.25 C 20.5 12.836 20.836 12.5 21.25 12.5 C 21.664 12.5 22 12.836 22 13.25 L 22 17.75 C 22 20.097 20.097 22 17.75 22 L 4.25 22 C 1.903 22 0 20.097 0 17.75 L 0 4.25 C 0 1.903 1.903 0 4.25 0 L 8.75 0 C 9.164 0 9.5 0.336 9.5 0.75 C 9.5 1.164 9.164 1.5 8.75 1.5 L 4.25 1.5 Z M 12.5 0.75 C 12.5 0.336 12.836 0 13.25 0 L 21.25 0 C 21.664 0 22 0.336 22 0.75 L 22 8.75 C 22 9.164 21.664 9.5 21.25 9.5 C 20.836 9.5 20.5 9.164 20.5 8.75 L 20.5 2.561 L 14.281 8.78 C 13.988 9.073 13.513 9.073 13.22 8.78 C 12.927 8.487 12.927 8.012 13.22 7.72 L 19.439 1.5 L 13.25 1.5 C 12.836 1.5 12.5 1.164 12.5 0.75 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 4 2 C 2.895 2 2 2.895 2 4 L 2 18 C 2 19.105 2.895 20 4 20 L 18 20 C 19.104 20 20 19.105 20 18 L 20 13.5 C 20 12.948 20.447 12.5 21 12.5 C 21.552 12.5 22 12.948 22 13.5 L 22 18 C 22 20.209 20.209 22 18 22 L 4 22 C 1.791 22 0 20.209 0 18 L 0 4 C 0 1.791 1.791 0 4 0 L 8.5 0 C 9.052 0 9.5 0.448 9.5 1 C 9.5 1.552 9.052 2 8.5 2 L 4 2 Z M 12.5 1 C 12.5 0.448 12.948 0 13.5 0 L 21 0 C 21.552 0 22 0.448 22 1 L 22 8.5 C 22 9.052 21.552 9.5 21 9.5 C 20.448 9.5 20 9.052 20 8.5 L 20 3.414 L 14.207 9.207 C 13.816 9.598 13.183 9.598 12.793 9.207 C 12.402 8.817 12.402 8.183 12.793 7.793 L 18.586 2 L 13.5 2 C 12.948 2 12.5 1.552 12.5 1 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 3.25 1.5 C 2.283 1.5 1.5 2.284 1.5 3.25 L 1.5 14.75 C 1.5 15.716 2.283 16.5 3.25 16.5 L 14.75 16.5 C 15.716 16.5 16.5 15.716 16.5 14.75 L 16.5 10.75 C 16.5 10.336 16.835 10 17.25 10 C 17.664 10 18 10.336 18 10.75 L 18 14.75 C 18 16.545 16.545 18 14.75 18 L 3.25 18 C 1.455 18 0 16.545 0 14.75 L 0 3.25 C 0 1.455 1.455 0 3.25 0 L 7.25 0 C 7.664 0 8 0.336 8 0.75 C 8 1.164 7.664 1.5 7.25 1.5 L 3.25 1.5 Z M 10 0.75 C 10 0.336 10.336 0 10.75 0 L 17.25 0 C 17.664 0 18 0.336 18 0.75 L 18 7.25 C 18 7.664 17.664 8 17.25 8 C 16.836 8 16.5 7.664 16.5 7.25 L 16.5 2.561 L 11.28 7.78 C 10.987 8.073 10.512 8.073 10.219 7.78 C 9.927 7.487 9.927 7.013 10.219 6.72 L 15.439 1.5 L 10.75 1.5 C 10.336 1.5 10 1.164 10 0.75 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 3.25 2 C 2.56 2 2 2.56 2 3.25 L 2 14.75 C 2 15.44 2.56 16 3.25 16 L 14.75 16 C 15.44 16 16 15.44 16 14.75 L 16 11 C 16 10.448 16.448 10 17 10 C 17.552 10 18 10.448 18 11 L 18 14.75 C 18 16.545 16.545 18 14.75 18 L 3.25 18 C 1.455 18 0 16.545 0 14.75 L 0 3.25 C 0 1.455 1.455 0 3.25 0 L 7 0 C 7.552 0 8 0.448 8 1 C 8 1.552 7.552 2 7 2 L 3.25 2 Z M 11 2 C 10.448 2 10 1.552 10 1 C 10 0.448 10.448 0 11 0 L 17 0 C 17.552 0 18 0.448 18 1 L 18 7 C 18 7.552 17.552 8 17 8 C 16.448 8 16 7.552 16 7 L 16 3.414 L 11.707 7.707 C 11.317 8.098 10.683 8.098 10.293 7.707 C 9.902 7.317 9.902 6.683 10.293 6.293 L 14.586 2 L 11 2 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 12.105 1.895 13 3 13 L 11 13 C 12.105 13 13 12.105 13 11 L 13 8.5 C 13 8.224 13.224 8 13.5 8 C 13.776 8 14 8.224 14 8.5 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 C 0 1.343 1.343 0 3 0 L 5.5 0 C 5.776 0 6 0.224 6 0.5 C 6 0.776 5.776 1 5.5 1 L 3 1 Z M 8 0.5 C 8 0.224 8.224 0 8.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 L 14 5.5 C 14 5.776 13.776 6 13.5 6 C 13.224 6 13 5.776 13 5.5 L 13 1.707 L 8.854 5.854 C 8.658 6.049 8.342 6.049 8.146 5.854 C 7.951 5.658 7.951 5.342 8.146 5.146 L 12.293 1 L 8.5 1 C 8.224 1 8 0.776 8 0.5 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 3.25 1.5 C 2.284 1.5 1.5 2.284 1.5 3.25 L 1.5 10.75 C 1.5 11.716 2.284 12.5 3.25 12.5 L 10.75 12.5 C 11.716 12.5 12.5 11.716 12.5 10.75 L 12.5 8.75 C 12.5 8.336 12.836 8 13.25 8 C 13.664 8 14 8.336 14 8.75 L 14 10.75 C 14 12.545 12.545 14 10.75 14 L 3.25 14 C 1.455 14 0 12.545 0 10.75 L 0 3.25 C 0 1.455 1.455 0 3.25 0 L 5.25 0 C 5.664 0 6 0.336 6 0.75 C 6 1.164 5.664 1.5 5.25 1.5 L 3.25 1.5 Z M 7.5 0.75 C 7.5 0.336 7.836 0 8.25 0 L 13.25 0 C 13.664 0 14 0.336 14 0.75 L 14 5.75 C 14 6.164 13.664 6.5 13.25 6.5 C 12.836 6.5 12.5 6.164 12.5 5.75 L 12.5 2.561 L 8.78 6.28 C 8.487 6.573 8.013 6.573 7.72 6.28 C 7.427 5.987 7.427 5.513 7.72 5.22 L 11.439 1.5 L 8.25 1.5 C 7.836 1.5 7.5 1.164 7.5 0.75 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 2.5 1 C 1.672 1 1 1.672 1 2.5 L 1 9.5 C 1 10.328 1.672 11 2.5 11 L 9.5 11 C 10.328 11 11 10.328 11 9.5 L 11 7.269 C 11 6.993 11.224 6.769 11.5 6.769 C 11.776 6.769 12 6.993 12 7.269 L 12 9.5 C 12 10.881 10.881 12 9.5 12 L 2.5 12 C 1.119 12 0 10.881 0 9.5 L 0 2.5 C 0 1.119 1.119 0 2.5 0 L 4.731 0 C 5.007 0 5.231 0.224 5.231 0.5 C 5.231 0.776 5.007 1 4.731 1 L 2.5 1 Z M 6.769 0.5 C 6.769 0.224 6.993 0 7.269 0 L 11.5 0 C 11.776 0 12 0.224 12 0.5 L 12 4.731 C 12 5.007 11.776 5.231 11.5 5.231 C 11.224 5.231 11 5.007 11 4.731 L 11 1.707 L 7.623 5.084 C 7.428 5.28 7.111 5.28 6.916 5.084 C 6.72 4.889 6.72 4.572 6.916 4.377 L 10.293 1 L 7.269 1 C 6.993 1 6.769 0.776 6.769 0.5 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 2.75 1.5 C 2.06 1.5 1.5 2.06 1.5 2.75 L 1.5 9.25 C 1.5 9.94 2.06 10.5 2.75 10.5 L 9.25 10.5 C 9.94 10.5 10.5 9.94 10.5 9.25 L 10.5 7.75 C 10.5 7.336 10.836 7 11.25 7 C 11.664 7 12 7.336 12 7.75 L 12 9.25 C 12 10.769 10.769 12 9.25 12 L 2.75 12 C 1.231 12 0 10.769 0 9.25 L 0 2.75 C 0 1.231 1.231 0 2.75 0 L 4.25 0 C 4.664 0 5 0.336 5 0.75 C 5 1.164 4.664 1.5 4.25 1.5 L 2.75 1.5 Z M 6 0.75 C 6 0.336 6.336 0 6.75 0 L 11.25 0 C 11.664 0 12 0.336 12 0.75 L 12 5.25 C 12 5.664 11.664 6 11.25 6 C 10.836 6 10.5 5.664 10.5 5.25 L 10.5 2.561 L 7.28 5.781 C 6.987 6.073 6.512 6.073 6.219 5.781 C 5.927 5.488 5.927 5.013 6.219 4.72 L 9.439 1.5 L 6.75 1.5 C 6.336 1.5 6 1.164 6 0.75 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={26.000} height={26} viewBox="0 0 26.000 26" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 3,
        width: 26,
        height: 26,
      }}>
        <path d={"M 5.5 2 C 3.567 2 2 3.567 2 5.5 L 2 20.5 C 2 22.433 3.567 24 5.5 24 L 20.5 24 C 22.433 24 24 22.433 24 20.5 L 24 16 C 24 15.448 24.448 15 25 15 C 25.552 15 26 15.448 26 16 L 26 20.5 C 26 23.538 23.538 26 20.5 26 L 5.5 26 C 2.462 26 0 23.538 0 20.5 L 0 5.5 C 0 2.462 2.462 0 5.5 0 L 10 0 C 10.552 0 11 0.448 11 1 C 11 1.552 10.552 2 10 2 L 5.5 2 Z M 15 1 C 15 0.448 15.448 0 16 0 L 25 0 C 25.553 0 26 0.448 26 1 L 26 10 C 26 10.552 25.553 11 25 11 C 24.448 11 24 10.552 24 10 L 24 3.414 L 16.707 10.707 C 16.317 11.098 15.683 11.098 15.293 10.707 C 14.902 10.317 14.902 9.684 15.293 9.293 L 22.586 2 L 16 2 C 15.448 2 15 1.552 15 1 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 4.75 2.5 C 3.507 2.5 2.5 3.507 2.5 4.75 L 2.5 21.25 C 2.5 22.493 3.507 23.5 4.75 23.5 L 21.25 23.5 C 22.493 23.5 23.5 22.493 23.5 21.25 L 23.5 16.25 C 23.5 15.56 24.06 15 24.75 15 C 25.44 15 26 15.56 26 16.25 L 26 21.25 C 26 23.873 23.873 26 21.25 26 L 4.75 26 C 2.127 26 0 23.873 0 21.25 L 0 4.75 C 0 2.127 2.127 0 4.75 0 L 9.75 0 C 10.44 0 11 0.56 11 1.25 C 11 1.94 10.44 2.5 9.75 2.5 L 4.75 2.5 Z M 15 1.25 C 15 0.56 15.56 0 16.25 0 L 24.75 0 C 25.44 0 26 0.56 26 1.25 L 26 9.75 C 26 10.44 25.44 11 24.75 11 C 24.06 11 23.5 10.44 23.5 9.75 L 23.5 4.268 L 17.134 10.634 C 16.646 11.122 15.854 11.122 15.366 10.634 C 14.878 10.146 14.878 9.354 15.366 8.866 L 21.732 2.5 L 16.25 2.5 C 15.56 2.5 15 1.94 15 1.25 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
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
        <path d={"M 2 1 C 1.448 1 1 1.448 1 2 L 1 6 C 1 6.552 1.448 7 2 7 L 6 7 C 6.552 7 7 6.552 7 6 L 7 5 C 7 4.724 7.224 4.5 7.5 4.5 C 7.776 4.5 8 4.724 8 5 L 8 6 C 8 7.105 7.105 8 6 8 L 2 8 C 0.895 8 0 7.105 0 6 L 0 2 C 0 0.895 0.895 0 2 0 L 3 0 C 3.276 0 3.5 0.224 3.5 0.5 C 3.5 0.776 3.276 1 3 1 L 2 1 Z M 5 1 C 4.724 1 4.5 0.776 4.5 0.5 C 4.5 0.224 4.724 0 5 0 L 7.5 0 C 7.776 0 8 0.224 8 0.5 L 8 3 C 8 3.276 7.776 3.5 7.5 3.5 C 7.224 3.5 7 3.276 7 3 L 7 1.707 L 5.354 3.354 C 5.158 3.549 4.842 3.549 4.646 3.354 C 4.451 3.158 4.451 2.842 4.646 2.646 L 6.293 1 L 5 1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
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
        <path d={"M 2 1.5 C 1.724 1.5 1.5 1.724 1.5 2 L 1.5 6 C 1.5 6.276 1.724 6.5 2 6.5 L 6 6.5 C 6.276 6.5 6.5 6.276 6.5 6 L 6.5 5.75 C 6.5 5.336 6.836 5 7.25 5 C 7.664 5 8 5.336 8 5.75 L 8 6 C 8 7.105 7.105 8 6 8 L 2 8 C 0.895 8 0 7.105 0 6 L 0 2 C 0 0.895 0.895 0 2 0 L 2.25 0 C 2.664 0 3 0.336 3 0.75 C 3 1.164 2.664 1.5 2.25 1.5 L 2 1.5 Z M 4.75 1.5 C 4.336 1.5 4 1.164 4 0.75 C 4 0.336 4.336 0 4.75 0 L 7.25 0 C 7.664 0 8 0.336 8 0.75 L 8 3.25 C 8 3.664 7.664 4 7.25 4 C 6.836 4 6.5 3.664 6.5 3.25 L 6.5 2.561 L 5.28 3.78 C 4.987 4.073 4.513 4.073 4.22 3.78 C 3.927 3.487 3.927 3.013 4.22 2.72 L 5.439 1.5 L 4.75 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body12,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body13,
  };
  return (__impls[__vkey(props)] ?? __body12)();
}
export default Open;
