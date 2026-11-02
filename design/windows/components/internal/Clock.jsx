import { __slateNorm } from './slateProps.jsx';
// figma node: 9116:27334 Clock (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Clock(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","28","32","48"],"theme":["regular","filled","light"]});
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
      <svg width={38} height={38} viewBox="0 0 38 38" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 5,
        width: 38,
        height: 38,
      }}>
        <path d={"M 19 0 C 29.493 0 38 8.507 38 19 C 38 29.493 29.493 38 19 38 C 8.507 38 0 29.493 0 19 C 0 8.507 8.507 0 19 0 Z M 19 2.5 C 9.887 2.5 2.5 9.887 2.5 19 C 2.5 28.113 9.887 35.5 19 35.5 C 28.113 35.5 35.5 28.113 35.5 19 C 35.5 9.887 28.113 2.5 19 2.5 Z M 17.75 7 C 18.397 7 18.93 7.492 18.994 8.122 L 19 8.25 L 19 19 L 25.75 19 C 26.44 19 27 19.56 27 20.25 C 27 20.897 26.508 21.43 25.878 21.494 L 25.75 21.5 L 17.75 21.5 C 17.103 21.5 16.57 21.008 16.506 20.378 L 16.5 20.25 L 16.5 8.25 C 16.5 7.56 17.06 7 17.75 7 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={38} height={38} viewBox="0 0 38 38" fill="none" style={{
        position: "absolute",
        left: 5,
        top: 5,
        width: 38,
        height: 38,
      }}>
        <path d={"M 19 0 C 29.493 0 38 8.507 38 19 C 38 29.493 29.493 38 19 38 C 8.507 38 0 29.493 0 19 C 0 8.507 8.507 0 19 0 Z M 17.75 7 C 17.06 7 16.5 7.56 16.5 8.25 L 16.5 20.25 C 16.5 20.94 17.06 21.5 17.75 21.5 L 25.75 21.5 C 26.44 21.5 27 20.94 27 20.25 C 27 19.56 26.44 19 25.75 19 L 19 19 L 19 8.25 C 19 7.56 18.44 7 17.75 7 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28,
      }}>
        <path d={"M 12 7 C 12 6.448 12.448 6 13 6 C 13.552 6 14 6.448 14 7 L 14 14 L 18 14 C 18.552 14 19 14.448 19 15 C 19 15.552 18.552 16 18 16 L 13 16 C 12.448 16 12 15.552 12 15 L 12 7 Z M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 14 26 C 7.373 26 2 20.627 2 14 C 2 7.373 7.373 2 14 2 C 20.627 2 26 7.373 26 14 C 26 20.627 20.627 26 14 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28,
      }}>
        <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 12 7 C 12 6.448 12.448 6 13 6 C 13.552 6 14 6.448 14 7 L 14 14 L 18 14 C 18.552 14 19 14.448 19 15 C 19 15.552 18.552 16 18 16 L 13 16 C 12.448 16 12 15.552 12 15 L 12 7 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 24,
        height: 24,
      }}>
        <path d={"M 1.5 12 C 1.5 6.201 6.201 1.5 12 1.5 C 17.799 1.5 22.5 6.201 22.5 12 C 22.5 17.799 17.799 22.5 12 22.5 C 6.201 22.5 1.5 17.799 1.5 12 Z M 12 0 C 5.373 0 0 5.373 0 12 C 0 18.627 5.373 24 12 24 C 18.627 24 24 18.627 24 12 C 24 5.373 18.627 0 12 0 Z M 11.993 5.648 C 11.943 5.282 11.63 5 11.25 5 C 10.836 5 10.5 5.336 10.5 5.75 L 10.5 12.75 L 10.507 12.852 C 10.557 13.218 10.87 13.5 11.25 13.5 L 16.25 13.5 L 16.352 13.493 C 16.718 13.443 17 13.13 17 12.75 C 17 12.336 16.664 12 16.25 12 L 12 12 L 12 5.75 L 11.993 5.648 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 24,
        height: 24,
      }}>
        <path d={"M 16.25 13.5 L 11.25 13.5 C 10.836 13.5 10.5 13.164 10.5 12.75 L 10.5 5.75 C 10.5 5.336 10.836 5 11.25 5 C 11.664 5 12 5.336 12 5.75 L 12 12 L 16.25 12 C 16.664 12 17 12.336 17 12.75 C 17 13.164 16.664 13.5 16.25 13.5 Z M 12 0 C 5.372 0 0 5.373 0 12 C 0 18.627 5.372 24 12 24 C 18.627 24 24 18.627 24 12 C 24 5.373 18.627 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
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
        <path d={"M 1.5 10 C 1.5 5.306 5.306 1.5 10 1.5 C 14.694 1.5 18.5 5.306 18.5 10 C 18.5 14.694 14.694 18.5 10 18.5 C 5.306 18.5 1.5 14.694 1.5 10 Z M 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 Z M 9.993 4.648 C 9.943 4.282 9.63 4 9.25 4 C 8.836 4 8.5 4.336 8.5 4.75 L 8.5 10.75 L 8.507 10.852 C 8.557 11.218 8.87 11.5 9.25 11.5 L 13.25 11.5 L 13.352 11.493 C 13.718 11.443 14 11.13 14 10.75 C 14 10.336 13.664 10 13.25 10 L 10 10 L 10 4.75 L 9.993 4.648 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
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
        <path d={"M 13.25 11.5 L 9.25 11.5 C 8.836 11.5 8.5 11.164 8.5 10.75 L 8.5 4.75 C 8.5 4.336 8.836 4 9.25 4 C 9.664 4 10 4.336 10 4.75 L 10 10 L 13.25 10 C 13.664 10 14 10.336 14 10.75 C 14 11.164 13.664 11.5 13.25 11.5 Z M 10 0 C 4.478 0 0 4.478 0 10 C 0 15.522 4.478 20 10 20 C 15.522 20 20 15.522 20 10 C 20 4.478 15.522 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
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
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 8 1 C 4.134 1 1 4.134 1 8 C 1 11.866 4.134 15 8 15 C 11.866 15 15 11.866 15 8 C 15 4.134 11.866 1 8 1 Z M 7.5 3 C 7.745 3 7.95 3.177 7.992 3.41 L 8 3.5 L 8 8 L 10.5 8 C 10.776 8 11 8.224 11 8.5 C 11 8.745 10.823 8.95 10.59 8.992 L 10.5 9 L 7.5 9 C 7.255 9 7.05 8.823 7.008 8.59 L 7 8.5 L 7 3.5 C 7 3.224 7.224 3 7.5 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
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
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 7.5 3 C 7.224 3 7 3.224 7 3.5 L 7 8.5 L 7.008 8.59 C 7.05 8.823 7.255 9 7.5 9 L 10.5 9 L 10.59 8.992 C 10.823 8.95 11 8.745 11 8.5 C 11 8.224 10.776 8 10.5 8 L 8 8 L 8 3.5 L 7.992 3.41 C 7.95 3.177 7.745 3 7.5 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 14,
        height: 14,
      }}>
        <path d={"M 1 7 C 1 3.686 3.686 1 7 1 C 10.314 1 13 3.686 13 7 C 13 10.314 10.314 13 7 13 C 3.686 13 1 10.314 1 7 Z M 7 0 C 3.134 0 0 3.134 0 7 C 0 10.866 3.134 14 7 14 C 10.866 14 14 10.866 14 7 C 14 3.134 10.866 0 7 0 Z M 7 3.5 C 7 3.224 6.776 3 6.5 3 C 6.224 3 6 3.224 6 3.5 L 6 7.5 C 6 7.776 6.224 8 6.5 8 L 9.5 8 C 9.776 8 10 7.776 10 7.5 C 10 7.224 9.776 7 9.5 7 L 7 7 L 7 3.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 14,
        height: 14,
      }}>
        <path d={"M 7 14 C 10.866 14 14 10.866 14 7 C 14 3.134 10.866 0 7 0 C 3.134 0 0 3.134 0 7 C 0 10.866 3.134 14 7 14 Z M 7 3.5 L 7 7 L 9.5 7 C 9.776 7 10 7.224 10 7.5 C 10 7.776 9.776 8 9.5 8 L 6.5 8 C 6.224 8 6 7.776 6 7.5 L 6 3.5 C 6 3.224 6.224 3 6.5 3 C 6.776 3 7 3.224 7 3.5 Z"} fill="currentColor" fillRule="nonzero" />
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
        <path d={"M 5 0 C 7.761 0 10 2.239 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 C 0 2.239 2.239 0 5 0 Z M 5 1 C 2.791 1 1 2.791 1 5 C 1 7.209 2.791 9 5 9 C 7.209 9 9 7.209 9 5 C 9 2.791 7.209 1 5 1 Z M 4.5 2.5 C 4.745 2.5 4.95 2.677 4.992 2.91 L 5 3 L 5 5 L 6.5 5 C 6.776 5 7 5.224 7 5.5 C 7 5.745 6.823 5.95 6.59 5.992 L 6.5 6 L 4.5 6 C 4.255 6 4.05 5.823 4.008 5.59 L 4 5.5 L 4 3 C 4 2.724 4.224 2.5 4.5 2.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 1,
        width: 10,
        height: 10,
      }}>
        <path d={"M 5 0 C 7.761 0 10 2.239 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 C 0 2.239 2.239 0 5 0 Z M 4.5 2.5 C 4.224 2.5 4 2.724 4 3 L 4 5.5 C 4 5.776 4.224 6 4.5 6 L 6.5 6 C 6.776 6 7 5.776 7 5.5 C 7 5.224 6.776 5 6.5 5 L 5 5 L 5 3 C 5 2.724 4.776 2.5 4.5 2.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 28,
        height: 28,
      }}>
        <path d={"M 14 6.5 C 14 6.224 13.776 6 13.5 6 C 13.224 6 13 6.224 13 6.5 L 13 14.5 C 13 14.776 13.224 15 13.5 15 L 18.5 15 C 18.776 15 19 14.776 19 14.5 C 19 14.224 18.776 14 18.5 14 L 14 14 L 14 6.5 Z M 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 Z M 27 14 C 27 6.82 21.18 1 14 1 C 6.82 1 1 6.82 1 14 C 1 21.18 6.82 27 14 27 C 21.18 27 27 21.18 27 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=48, Theme=Regular
    "size=48|theme=regular": __body0,
    // figma: Size=48, Theme=Filled
    "size=48|theme=filled": __body1,
    // figma: Size=32, Theme=Regular
    "size=32|theme=regular": __body2,
    // figma: Size=32, Theme=Filled
    "size=32|theme=filled": __body3,
    // figma: Size=28, Theme=Regular
    "size=28|theme=regular": __body4,
    // figma: Size=28, Theme=Filled
    "size=28|theme=filled": __body5,
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body6,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body7,
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body8,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body9,
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body10,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body11,
    // figma: Size=12, Theme=Regular
    "size=12|theme=regular": __body12,
    // figma: Size=12, Theme=Filled
    "size=12|theme=filled": __body13,
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body14,
  };
  return (__impls[__vkey(props)] ?? __body12)();
}
export default Clock;
