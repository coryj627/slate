import { __slateNorm } from './slateProps.jsx';
// figma node: 9064:4495 Error Circle (10 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function ErrorCircle(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","48"],"theme":["regular","filled"]});
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
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 20,
        height: 20,
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.478 20 10 C 20 15.522 15.523 20 10 20 C 4.477 20 0 15.522 0 10 C 0 4.478 4.477 0 10 0 Z M 10 1.667 C 5.405 1.667 1.667 5.405 1.667 10 C 1.667 14.595 5.405 18.333 10 18.333 C 14.595 18.333 18.333 14.595 18.333 10 C 18.333 5.405 14.595 1.667 10 1.667 Z M 9.999 12.502 C 10.55 12.502 10.997 12.949 10.997 13.501 C 10.997 14.052 10.55 14.5 9.999 14.5 C 9.447 14.5 9 14.052 9 13.501 C 9 12.949 9.447 12.502 9.999 12.502 Z M 9.994 5 C 10.374 5 10.688 5.282 10.738 5.648 L 10.745 5.749 L 10.749 10.251 C 10.749 10.665 10.413 11.001 9.999 11.002 C 9.62 11.002 9.306 10.72 9.256 10.354 L 9.249 10.252 L 9.245 5.751 C 9.245 5.336 9.58 5 9.994 5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 20,
        height: 20,
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.478 20 10 C 20 15.522 15.523 20 10 20 C 4.477 20 0 15.522 0 10 C 0 4.478 4.477 0 10 0 Z M 10.002 13.004 C 9.45 13.004 9.003 13.451 9.003 14.002 C 9.003 14.554 9.45 15.001 10.002 15.001 C 10.553 15.001 11 14.554 11 14.002 C 11 13.451 10.553 13.004 10.002 13.004 Z M 10 5 C 9.487 5 9.064 5.386 9.007 5.884 L 9 6 L 9.002 11.001 L 9.009 11.118 C 9.067 11.615 9.489 12.001 10.002 12.001 C 10.515 12.001 10.938 11.615 10.995 11.117 L 11.002 11.001 L 11 6 L 10.993 5.883 C 10.935 5.386 10.512 5 10 5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 8 1 C 4.134 1 1 4.134 1 8 C 1 11.866 4.134 15 8 15 C 11.866 15 15 11.866 15 8 C 15 4.134 11.866 1 8 1 Z M 8 10.5 C 8.414 10.5 8.75 10.836 8.75 11.25 C 8.75 11.664 8.414 12 8 12 C 7.586 12 7.25 11.664 7.25 11.25 C 7.25 10.836 7.586 10.5 8 10.5 Z M 8 4 C 8.245 4 8.45 4.177 8.492 4.41 L 8.5 4.5 L 8.5 9 C 8.5 9.276 8.276 9.5 8 9.5 C 7.755 9.5 7.55 9.323 7.508 9.09 L 7.5 9 L 7.5 4.5 C 7.5 4.224 7.724 4 8 4 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 8 10.5 C 7.586 10.5 7.25 10.836 7.25 11.25 C 7.25 11.664 7.586 12 8 12 C 8.414 12 8.75 11.664 8.75 11.25 C 8.75 10.836 8.414 10.5 8 10.5 Z M 8 4 C 7.755 4 7.55 4.177 7.508 4.41 L 7.5 4.5 L 7.5 9 L 7.508 9.09 C 7.55 9.323 7.755 9.5 8 9.5 C 8.245 9.5 8.45 9.323 8.492 9.09 L 8.5 9 L 8.5 4.5 L 8.492 4.41 C 8.45 4.177 8.245 4 8 4 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 12,
        height: 12,
      }}>
        <path d={"M 6 0 C 9.314 0 12 2.686 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 C 0 2.686 2.686 0 6 0 Z M 6 1 C 3.239 1 1 3.239 1 6 C 1 8.761 3.239 11 6 11 C 8.761 11 11 8.761 11 6 C 11 3.239 8.761 1 6 1 Z M 6 8 C 6.414 8 6.75 8.336 6.75 8.75 C 6.75 9.164 6.414 9.5 6 9.5 C 5.586 9.5 5.25 9.164 5.25 8.75 C 5.25 8.336 5.586 8 6 8 Z M 6 2.5 C 6.245 2.5 6.45 2.677 6.492 2.91 L 6.5 3 L 6.5 6.5 C 6.5 6.776 6.276 7 6 7 C 5.755 7 5.55 6.823 5.508 6.59 L 5.5 6.5 L 5.5 3 C 5.5 2.724 5.724 2.5 6 2.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 12,
        height: 12,
      }}>
        <path d={"M 6 0 C 9.314 0 12 2.686 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 C 0 2.686 2.686 0 6 0 Z M 6 8 C 5.586 8 5.25 8.336 5.25 8.75 C 5.25 9.164 5.586 9.5 6 9.5 C 6.414 9.5 6.75 9.164 6.75 8.75 C 6.75 8.336 6.414 8 6 8 Z M 6 2.5 C 5.755 2.5 5.55 2.677 5.508 2.91 L 5.5 3 L 5.5 6.5 L 5.508 6.59 C 5.55 6.823 5.755 7 6 7 C 6.245 7 6.45 6.823 6.492 6.59 L 6.5 6.5 L 6.5 3 L 6.492 2.91 C 6.45 2.677 6.245 2.5 6 2.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
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
        <path d={"M 4.25 7.25 C 4.25 6.836 4.586 6.5 5 6.5 C 5.414 6.5 5.75 6.836 5.75 7.25 C 5.75 7.664 5.414 8 5 8 C 4.586 8 4.25 7.664 4.25 7.25 Z M 4.508 2.41 C 4.55 2.177 4.755 2 5 2 C 5.245 2 5.45 2.177 5.492 2.41 L 5.5 2.5 L 5.5 5 L 5.492 5.09 C 5.45 5.323 5.245 5.5 5 5.5 C 4.755 5.5 4.55 5.323 4.508 5.09 L 4.5 5 L 4.5 2.5 L 4.508 2.41 Z M 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 Z M 9 5 C 9 2.791 7.209 1 5 1 C 2.791 1 1 2.791 1 5 C 1 7.209 2.791 9 5 9 C 7.209 9 9 7.209 9 5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
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
        <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 4.25 7.25 C 4.25 6.836 4.586 6.5 5 6.5 C 5.414 6.5 5.75 6.836 5.75 7.25 C 5.75 7.664 5.414 8 5 8 C 4.586 8 4.25 7.664 4.25 7.25 Z M 4.508 2.41 C 4.55 2.177 4.755 2 5 2 C 5.245 2 5.45 2.177 5.492 2.41 L 5.5 2.5 L 5.5 5 L 5.492 5.09 C 5.45 5.323 5.245 5.5 5 5.5 C 4.755 5.5 4.55 5.323 4.508 5.09 L 4.5 5 L 4.5 2.5 L 4.508 2.41 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
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
        <path d={"M 20 9 C 20.69 9 21.25 9.56 21.25 10.25 L 21.25 22.75 C 21.25 23.44 20.69 24 20 24 C 19.31 24 18.75 23.44 18.75 22.75 L 18.75 10.25 C 18.75 9.56 19.31 9 20 9 Z M 20 30 C 21.105 30 22 29.105 22 28 C 22 26.895 21.105 26 20 26 C 18.895 26 18 26.895 18 28 C 18 29.105 18.895 30 20 30 Z M 0 20 C 0 8.954 8.954 0 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 Z M 20 2.5 C 10.335 2.5 2.5 10.335 2.5 20 C 2.5 29.665 10.335 37.5 20 37.5 C 29.665 37.5 37.5 29.665 37.5 20 C 37.5 10.335 29.665 2.5 20 2.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
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
        <path d={"M 20 0 C 8.954 0 0 8.954 0 20 C 0 31.046 8.954 40 20 40 C 31.046 40 40 31.046 40 20 C 40 8.954 31.046 0 20 0 Z M 20 9 C 20.69 9 21.25 9.56 21.25 10.25 L 21.25 22.75 C 21.25 23.44 20.69 24 20 24 C 19.31 24 18.75 23.44 18.75 22.75 L 18.75 10.25 C 18.75 9.56 19.31 9 20 9 Z M 20 30 C 18.895 30 18 29.105 18 28 C 18 26.895 18.895 26 20 26 C 21.105 26 22 26.895 22 28 C 22 29.105 21.105 30 20 30 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body8,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body9,
  };
  return (__impls[__vkey(props)] ?? __body6)();
}
export default ErrorCircle;
