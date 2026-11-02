import { __slateNorm } from './slateProps.jsx';
// figma node: 9315:2 Filter (13 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function Filter(_p = {}) {
  _p = __slateNorm(_p, {"size":["12","16","20","24","28","32"],"theme":["regular","filled","light"]});
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
      <svg width={22} height={13.500} viewBox="0 0 22 13.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 7,
        width: 22,
        height: 13.5,
      }}>
        <path d={"M 14.25 12 C 14.664 12 15 12.336 15 12.75 C 15 13.164 14.664 13.5 14.25 13.5 L 7.75 13.5 C 7.336 13.5 7 13.164 7 12.75 C 7 12.336 7.336 12 7.75 12 L 14.25 12 Z M 18.25 6 C 18.664 6 19 6.336 19 6.75 C 19 7.164 18.664 7.5 18.25 7.5 L 3.75 7.5 C 3.336 7.5 3 7.164 3 6.75 C 3 6.336 3.336 6 3.75 6 L 18.25 6 Z M 21.25 0 C 21.664 0 22 0.336 22 0.75 C 22 1.164 21.664 1.5 21.25 1.5 L 0.75 1.5 C 0.336 1.5 0 1.164 0 0.75 C 0 0.336 0.336 0 0.75 0 L 21.25 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={22} height={14} viewBox="0 0 22 14" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 7,
        width: 22,
        height: 14,
      }}>
        <path d={"M 14 12 C 14.552 12 15 12.448 15 13 C 15 13.552 14.552 14 14 14 L 8 14 C 7.448 14 7 13.552 7 13 C 7 12.448 7.448 12 8 12 L 14 12 Z M 18 6 C 18.552 6 19 6.448 19 7 C 19 7.552 18.552 8 18 8 L 4 8 C 3.448 8 3 7.552 3 7 C 3 6.448 3.448 6 4 6 L 18 6 Z M 21 0 C 21.552 0 22 0.448 22 1 C 22 1.552 21.552 2 21 2 L 1 2 C 0.448 2 0 1.552 0 1 C 0 0.448 0.448 0 1 0 L 21 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16.500} height={11.500} viewBox="0 0 16.500 11.500" fill="none" style={{
        position: "absolute",
        left: 3.75,
        top: 6,
        width: 16.5,
        height: 11.5,
      }}>
        <path d={"M 9.75 10 C 10.164 10 10.5 10.336 10.5 10.75 C 10.5 11.164 10.164 11.5 9.75 11.5 L 6.75 11.5 C 6.336 11.5 6 11.164 6 10.75 C 6 10.336 6.336 10 6.75 10 L 9.75 10 Z M 12.75 5 C 13.164 5 13.5 5.336 13.5 5.75 C 13.5 6.164 13.164 6.5 12.75 6.5 L 3.75 6.5 C 3.336 6.5 3 6.164 3 5.75 C 3 5.336 3.336 5 3.75 5 L 12.75 5 Z M 15.75 0 C 16.164 0 16.5 0.336 16.5 0.75 C 16.5 1.164 16.164 1.5 15.75 1.5 L 0.75 1.5 C 0.336 1.5 0 1.164 0 0.75 C 0 0.336 0.336 0 0.75 0 L 15.75 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={12} viewBox="0 0 16 12" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 6,
        width: 16,
        height: 12,
      }}>
        <path d={"M 6 10 L 10 10 C 10.552 10 11 10.448 11 11 C 11 11.513 10.614 11.936 10.117 11.993 L 10 12 L 6 12 C 5.448 12 5 11.552 5 11 C 5 10.487 5.386 10.064 5.883 10.007 L 6 10 L 10 10 L 6 10 Z M 4 5 L 12 5 C 12.552 5 13 5.448 13 6 C 13 6.513 12.614 6.936 12.117 6.993 L 12 7 L 4 7 C 3.448 7 3 6.552 3 6 C 3 5.487 3.386 5.064 3.883 5.007 L 4 5 L 12 5 L 4 5 Z M 1 0 L 15 0 C 15.552 0 16 0.448 16 1 C 16 1.513 15.614 1.936 15.117 1.993 L 15 2 L 1 2 C 0.448 2 0 1.552 0 1 C 0 0.487 0.386 0.064 0.883 0.007 L 1 0 L 15 0 L 1 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14} height={9} viewBox="0 0 14 9" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 14,
        height: 9,
      }}>
        <path d={"M 4.5 8 L 9.5 8 C 9.776 8 10 8.224 10 8.5 C 10 8.745 9.823 8.95 9.59 8.992 L 9.5 9 L 4.5 9 C 4.224 9 4 8.776 4 8.5 C 4 8.255 4.177 8.05 4.41 8.008 L 4.5 8 L 9.5 8 L 4.5 8 Z M 2.5 4 L 11.5 4 C 11.776 4 12 4.224 12 4.5 C 12 4.745 11.823 4.95 11.59 4.992 L 11.5 5 L 2.5 5 C 2.224 5 2 4.776 2 4.5 C 2 4.255 2.177 4.05 2.41 4.008 L 2.5 4 L 11.5 4 L 2.5 4 Z M 0.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 C 14 0.745 13.823 0.95 13.59 0.992 L 13.5 1 L 0.5 1 C 0.224 1 0 0.776 0 0.5 C 0 0.255 0.177 0.05 0.41 0.008 L 0.5 0 L 13.5 0 L 0.5 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={14} height={10} viewBox="0 0 14 10" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 14,
        height: 10,
      }}>
        <path d={"M 9.25 8.5 C 9.664 8.5 10 8.836 10 9.25 C 10 9.664 9.664 10 9.25 10 L 4.75 10 C 4.336 10 4 9.664 4 9.25 C 4 8.836 4.336 8.5 4.75 8.5 L 9.25 8.5 Z M 11.25 4.25 C 11.664 4.25 12 4.586 12 5 C 12 5.414 11.664 5.75 11.25 5.75 L 2.75 5.75 C 2.336 5.75 2 5.414 2 5 C 2 4.586 2.336 4.25 2.75 4.25 L 11.25 4.25 Z M 13.25 0 C 13.664 0 14 0.336 14 0.75 C 14 1.164 13.664 1.5 13.25 1.5 L 0.75 1.5 C 0.336 1.5 0 1.164 0 0.75 C 0 0.336 0.336 0 0.75 0 L 13.25 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12} height={9} viewBox="0 0 12 9" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 3,
        width: 12,
        height: 9,
      }}>
        <path d={"M 0 0.5 C 0 0.224 0.224 0 0.5 0 L 11.5 0 C 11.776 0 12 0.224 12 0.5 C 12 0.776 11.776 1 11.5 1 L 0.5 1 C 0.224 1 0 0.776 0 0.5 Z M 2 4.5 C 2 4.224 2.224 4 2.5 4 L 9.5 4 C 9.776 4 10 4.224 10 4.5 C 10 4.776 9.776 5 9.5 5 L 2.5 5 C 2.224 5 2 4.776 2 4.5 Z M 4 8.5 C 4 8.224 4.224 8 4.5 8 L 7.5 8 C 7.776 8 8 8.224 8 8.5 C 8 8.776 7.776 9 7.5 9 L 4.5 9 C 4.224 9 4 8.776 4 8.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={12.500} height={9.500} viewBox="0 0 12.500 9.500" fill="none" style={{
        position: "absolute",
        left: 1.75,
        top: 3,
        width: 12.5,
        height: 9.5,
      }}>
        <path d={"M 0 0.75 C 0 0.336 0.336 0 0.75 0 L 11.75 0 C 12.164 0 12.5 0.336 12.5 0.75 C 12.5 1.164 12.164 1.5 11.75 1.5 L 0.75 1.5 C 0.336 1.5 0 1.164 0 0.75 Z M 2 4.75 C 2 4.336 2.336 4 2.75 4 L 9.75 4 C 10.164 4 10.5 4.336 10.5 4.75 C 10.5 5.164 10.164 5.5 9.75 5.5 L 2.75 5.5 C 2.336 5.5 2 5.164 2 4.75 Z M 4 8.75 C 4 8.336 4.336 8 4.75 8 L 7.75 8 C 8.164 8 8.5 8.336 8.5 8.75 C 8.5 9.164 8.164 9.5 7.75 9.5 L 4.75 9.5 C 4.336 9.5 4 9.164 4 8.75 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={7} viewBox="0 0 10 7" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 2,
        width: 10,
        height: 7,
      }}>
        <path d={"M 0 0.5 C 0 0.224 0.224 0 0.5 0 L 9.5 0 C 9.776 0 10 0.224 10 0.5 C 10 0.776 9.776 1 9.5 1 L 0.5 1 C 0.224 1 0 0.776 0 0.5 Z M 2 3.5 C 2 3.224 2.224 3 2.5 3 L 7.5 3 C 7.776 3 8 3.224 8 3.5 C 8 3.776 7.776 4 7.5 4 L 2.5 4 C 2.224 4 2 3.776 2 3.5 Z M 4 6 C 3.724 6 3.5 6.224 3.5 6.5 C 3.5 6.776 3.724 7 4 7 L 6 7 C 6.276 7 6.5 6.776 6.5 6.5 C 6.5 6.224 6.276 6 6 6 L 4 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 12,
      height: 12,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={7.500} viewBox="0 0 10 7.500" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 2,
        width: 10,
        height: 7.5,
      }}>
        <path d={"M 0 0.75 C 0 0.336 0.336 0 0.75 0 L 9.25 0 C 9.664 0 10 0.336 10 0.75 C 10 1.164 9.664 1.5 9.25 1.5 L 0.75 1.5 C 0.336 1.5 0 1.164 0 0.75 Z M 2 3.75 C 2 3.336 2.336 3 2.75 3 L 7.25 3 C 7.664 3 8 3.336 8 3.75 C 8 4.164 7.664 4.5 7.25 4.5 L 2.75 4.5 C 2.336 4.5 2 4.164 2 3.75 Z M 4.25 6 C 3.836 6 3.5 6.336 3.5 6.75 C 3.5 7.164 3.836 7.5 4.25 7.5 L 5.75 7.5 C 6.164 7.5 6.5 7.164 6.5 6.75 C 6.5 6.336 6.164 6 5.75 6 L 4.25 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={26} height={16} viewBox="0 0 26 16" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 8,
        width: 26,
        height: 16,
      }}>
        <path d={"M 0 1 C 0 0.448 0.448 0 1 0 L 25 0 C 25.552 0 26 0.448 26 1 C 26 1.552 25.552 2 25 2 L 1 2 C 0.448 2 0 1.552 0 1 Z M 4 8 C 4 7.448 4.448 7 5 7 L 21 7 C 21.552 7 22 7.448 22 8 C 22 8.552 21.552 9 21 9 L 5 9 C 4.448 9 4 8.552 4 8 Z M 9 14 C 8.448 14 8 14.448 8 15 C 8 15.552 8.448 16 9 16 L 17 16 C 17.552 16 18 15.552 18 15 C 18 14.448 17.552 14 17 14 L 9 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={26} height={16.500} viewBox="0 0 26 16.500" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 8,
        width: 26,
        height: 16.5,
      }}>
        <path d={"M 0 1.25 C 0 0.56 0.56 0 1.25 0 L 24.75 0 C 25.44 0 26 0.56 26 1.25 C 26 1.94 25.44 2.5 24.75 2.5 L 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 Z M 4 8.25 C 4 7.56 4.56 7 5.25 7 L 20.75 7 C 21.44 7 22 7.56 22 8.25 C 22 8.94 21.44 9.5 20.75 9.5 L 5.25 9.5 C 4.56 9.5 4 8.94 4 8.25 Z M 9.25 14 C 8.56 14 8 14.56 8 15.25 C 8 15.94 8.56 16.5 9.25 16.5 L 16.75 16.5 C 17.44 16.5 18 15.94 18 15.25 C 18 14.56 17.44 14 16.75 14 L 9.25 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={26} height={15} viewBox="0 0 26 15" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 8,
        width: 26,
        height: 15,
      }}>
        <path d={"M 0 0.5 C 0 0.224 0.224 0 0.5 0 L 25.5 0 C 25.776 0 26 0.224 26 0.5 C 26 0.776 25.776 1 25.5 1 L 0.5 1 C 0.224 1 0 0.776 0 0.5 Z M 4 7.5 C 4 7.224 4.224 7 4.5 7 L 21.5 7 C 21.776 7 22 7.224 22 7.5 C 22 7.776 21.776 8 21.5 8 L 4.5 8 C 4.224 8 4 7.776 4 7.5 Z M 8.5 14 C 8.224 14 8 14.224 8 14.5 C 8 14.776 8.224 15 8.5 15 L 17.5 15 C 17.776 15 18 14.776 18 14.5 C 18 14.224 17.776 14 17.5 14 L 8.5 14 Z"} fill="currentColor" fillRule="nonzero" />
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
    // figma: Size=32, Theme=Light
    "size=32|theme=light": __body12,
  };
  return (__impls[__vkey(props)] ?? __body8)();
}
export default Filter;
