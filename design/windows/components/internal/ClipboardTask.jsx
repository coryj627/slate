import { __slateNorm } from './slateProps.jsx';
// figma node: 9121:7459 Clipboard Task (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "theme=" + __venc(p.theme);

export function ClipboardTask(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24"],"theme":["regular","filled"]});
  const props = { ..._p, size: _p.size ?? "16", theme: _p.theme ?? "regular" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={16} viewBox="0 0 12 16" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 12,
        height: 16,
      }}>
        <path d={"M 8.854 7.854 C 9.049 7.658 9.049 7.342 8.854 7.146 C 8.658 6.951 8.342 6.951 8.146 7.146 L 5 10.293 L 3.854 9.146 C 3.658 8.951 3.342 8.951 3.146 9.146 C 2.951 9.342 2.951 9.658 3.146 9.854 L 4.646 11.354 C 4.842 11.549 5.158 11.549 5.354 11.354 L 8.854 7.854 Z M 4.5 0 C 3.847 0 3.291 0.417 3.085 1 L 1.5 1 C 0.672 1 0 1.672 0 2.5 L 0 14.5 C 0 15.328 0.672 16 1.5 16 L 10.5 16 C 11.328 16 12 15.328 12 14.5 L 12 2.5 C 12 1.672 11.328 1 10.5 1 L 8.915 1 C 8.709 0.417 8.153 0 7.5 0 L 4.5 0 Z M 4 1.5 C 4 1.224 4.224 1 4.5 1 L 7.5 1 C 7.776 1 8 1.224 8 1.5 C 8 1.776 7.776 2 7.5 2 L 4.5 2 C 4.224 2 4 1.776 4 1.5 Z M 1.5 2 L 3.085 2 C 3.291 2.583 3.847 3 4.5 3 L 7.5 3 C 8.153 3 8.709 2.583 8.915 2 L 10.5 2 C 10.776 2 11 2.224 11 2.5 L 11 14.5 C 11 14.776 10.776 15 10.5 15 L 1.5 15 C 1.224 15 1 14.776 1 14.5 L 1 2.5 C 1 2.224 1.224 2 1.5 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={12} height={16} viewBox="0 0 12 16" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 12,
        height: 16,
      }}>
        <path d={"M 4.5 0 C 3.847 0 3.291 0.417 3.085 1 L 1.5 1 C 0.672 1 0 1.672 0 2.5 L 0 14.5 C 0 15.328 0.672 16 1.5 16 L 10.5 16 C 11.328 16 12 15.328 12 14.5 L 12 2.5 C 12 1.672 11.328 1 10.5 1 L 8.915 1 C 8.709 0.417 8.153 0 7.5 0 L 4.5 0 Z M 4 1.5 C 4 1.224 4.224 1 4.5 1 L 7.5 1 C 7.776 1 8 1.224 8 1.5 C 8 1.776 7.776 2 7.5 2 L 4.5 2 C 4.224 2 4 1.776 4 1.5 Z M 8.854 7.854 L 5.354 11.354 C 5.158 11.549 4.842 11.549 4.646 11.354 L 3.146 9.854 C 2.951 9.658 2.951 9.342 3.146 9.146 C 3.342 8.951 3.658 8.951 3.854 9.146 L 5 10.293 L 8.146 7.146 C 8.342 6.951 8.658 6.951 8.854 7.146 C 9.049 7.342 9.049 7.658 8.854 7.854 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={20} viewBox="0 0 16 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 16,
        height: 20,
      }}>
        <path d={"M 13.03 9.03 C 13.323 8.737 13.323 8.263 13.03 7.97 C 12.737 7.677 12.263 7.677 11.97 7.97 L 7 12.939 L 5.03 10.97 C 4.737 10.677 4.263 10.677 3.97 10.97 C 3.677 11.263 3.677 11.737 3.97 12.03 L 6.47 14.53 C 6.763 14.823 7.237 14.823 7.53 14.53 L 13.03 9.03 Z M 11.994 2.084 C 11.909 0.919 10.937 0 9.75 0 L 6.25 0 C 5.092 0 4.138 0.875 4.014 1.999 L 2.25 2 C 1.007 2 0 3.007 0 4.25 L 0 17.75 C 0 18.993 1.007 20 2.25 20 L 13.75 20 C 14.993 20 16 18.993 16 17.75 L 16 4.25 C 16 3.007 14.993 2 13.75 2 L 11.986 1.999 C 11.989 2.028 11.992 2.056 11.994 2.084 Z M 11.995 2.096 L 12 2.25 C 12 2.198 11.998 2.147 11.995 2.096 Z M 6.25 4.5 L 9.75 4.5 C 10.53 4.5 11.217 4.103 11.621 3.5 L 13.75 3.5 C 14.164 3.5 14.5 3.836 14.5 4.25 L 14.5 17.75 C 14.5 18.164 14.164 18.5 13.75 18.5 L 2.25 18.5 C 1.836 18.5 1.5 18.164 1.5 17.75 L 1.5 4.25 C 1.5 3.836 1.836 3.5 2.25 3.5 L 4.379 3.5 C 4.783 4.103 5.47 4.5 6.25 4.5 Z M 6.25 1.5 L 9.75 1.5 C 10.164 1.5 10.5 1.836 10.5 2.25 C 10.5 2.664 10.164 3 9.75 3 L 6.25 3 C 5.836 3 5.5 2.664 5.5 2.25 C 5.5 1.836 5.836 1.5 6.25 1.5 Z"} fill="currentColor" fillRule="nonzero" />
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
      <svg width={16} height={20} viewBox="0 0 16 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 2,
        width: 16,
        height: 20,
      }}>
        <path d={"M 9.75 0 L 6.25 0 C 5.092 0 4.138 0.875 4.014 1.999 L 2.25 2 C 1.007 2 0 3.007 0 4.25 L 0 17.75 C 0 18.993 1.007 20 2.25 20 L 13.75 20 C 14.993 20 16 18.993 16 17.75 L 16 4.25 C 16 3.007 14.993 2 13.75 2 L 11.986 1.999 C 11.862 0.875 10.908 0 9.75 0 Z M 6.25 1.5 L 9.75 1.5 C 10.164 1.5 10.5 1.836 10.5 2.25 C 10.5 2.664 10.164 3 9.75 3 L 6.25 3 C 5.836 3 5.5 2.664 5.5 2.25 C 5.5 1.836 5.836 1.5 6.25 1.5 Z M 13.03 9.03 L 7.53 14.53 C 7.237 14.823 6.763 14.823 6.47 14.53 L 3.97 12.03 C 3.677 11.737 3.677 11.263 3.97 10.97 C 4.263 10.677 4.737 10.677 5.03 10.97 L 7 12.939 L 11.97 7.97 C 12.263 7.677 12.737 7.677 13.03 7.97 C 13.323 8.263 13.323 8.737 13.03 9.03 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={14} viewBox="0 0 10 14" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 1,
        width: 10,
        height: 14,
      }}>
        <path d={"M 3.5 0 C 2.847 0 2.291 0.417 2.085 1 L 1.5 1 C 0.672 1 0 1.672 0 2.5 L 0 12.5 C 0 13.328 0.672 14 1.5 14 L 8.5 14 C 9.328 14 10 13.328 10 12.5 L 10 2.5 C 10 1.672 9.328 1 8.5 1 L 7.915 1 C 7.709 0.417 7.153 0 6.5 0 L 3.5 0 Z M 3 1.5 C 3 1.224 3.224 1 3.5 1 L 6.5 1 C 6.776 1 7 1.224 7 1.5 C 7 1.776 6.776 2 6.5 2 L 3.5 2 C 3.224 2 3 1.776 3 1.5 Z M 1.5 2 L 2.085 2 C 2.291 2.583 2.847 3 3.5 3 L 6.5 3 C 7.153 3 7.709 2.583 7.915 2 L 8.5 2 C 8.776 2 9 2.224 9 2.5 L 9 12.5 C 9 12.776 8.776 13 8.5 13 L 1.5 13 C 1.224 13 1 12.776 1 12.5 L 1 2.5 C 1 2.224 1.224 2 1.5 2 Z M 7.854 6.854 C 8.049 6.658 8.049 6.342 7.854 6.146 C 7.658 5.951 7.342 5.951 7.146 6.146 L 4.5 8.793 L 3.354 7.646 C 3.158 7.451 2.842 7.451 2.646 7.646 C 2.451 7.842 2.451 8.158 2.646 8.354 L 4.146 9.854 C 4.342 10.049 4.658 10.049 4.854 9.854 L 7.854 6.854 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      color: "inherit",
      ...props.style,
    }}>
      <svg width={10} height={14} viewBox="0 0 10 14" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 1,
        width: 10,
        height: 14,
      }}>
        <path d={"M 3.5 0 C 2.847 0 2.291 0.417 2.085 1 L 1.5 1 C 0.672 1 0 1.672 0 2.5 L 0 12.5 C 0 13.328 0.672 14 1.5 14 L 8.5 14 C 9.328 14 10 13.328 10 12.5 L 10 2.5 C 10 1.672 9.328 1 8.5 1 L 7.915 1 C 7.709 0.417 7.153 0 6.5 0 L 3.5 0 Z M 3 1.5 C 3 1.224 3.224 1 3.5 1 L 6.5 1 C 6.776 1 7 1.224 7 1.5 C 7 1.776 6.776 2 6.5 2 L 3.5 2 C 3.224 2 3 1.776 3 1.5 Z M 7.854 6.854 L 4.854 9.854 C 4.658 10.049 4.342 10.049 4.146 9.854 L 2.646 8.354 C 2.451 8.158 2.451 7.842 2.646 7.646 C 2.842 7.451 3.158 7.451 3.354 7.646 L 4.5 8.793 L 7.146 6.146 C 7.342 5.951 7.658 5.951 7.854 6.146 C 8.049 6.342 8.049 6.658 7.854 6.854 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: Size=20, Theme=Regular
    "size=20|theme=regular": __body0,
    // figma: Size=20, Theme=Filled
    "size=20|theme=filled": __body1,
    // figma: Size=24, Theme=Regular
    "size=24|theme=regular": __body2,
    // figma: Size=24, Theme=Filled
    "size=24|theme=filled": __body3,
    // figma: Size=16, Theme=Regular
    "size=16|theme=regular": __body4,
    // figma: Size=16, Theme=Filled
    "size=16|theme=filled": __body5,
  };
  return (__impls[__vkey(props)] ?? __body4)();
}
export default ClipboardTask;
