import { __slateNorm } from './slateProps.jsx';
// figma node: 9249:3293 folder (22 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "itemCount=" + __venc(p.itemCount) + '|' + "preview=" + __venc(p.preview);

export function Folder(_p = {}) {
  _p = __slateNorm(_p, {"size":["16","20","24","32","36","40","48","96","256"],"preview":["none","filled","thumbnail"]});
  const props = { ..._p, size: _p.size ?? "16", itemCount: _p.itemCount ?? true, preview: _p.preview ?? "none" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={84} height={60} viewBox="0 0 84 60" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 18,
        width: 84,
        height: 60,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 39 6 L 35.05 2.05 C 33.737 0.737 31.957 0 30.1 0 L 4 0 C 1.791 0 0 1.791 0 4 L 0 56 C 0 58.209 1.791 60 4 60 L 80 60 C 82.209 60 84 58.209 84 56 L 84 10 C 84 7.791 82.209 6 80 6 L 39 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <span style={{
        position: "absolute",
        left: 12,
        top: 58,
        width: 50,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "100%",
        color: "rgb(191,87,18)",
      }}>{props.text1 ?? "0"}</span>
      <svg width={82.401} height={1.602} viewBox="0 0 82.401 1.602" fill="none" style={{
        position: "absolute",
        left: 6.799,
        top: 76.398,
        width: 82.401,
        height: 1.602,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 3.201 1.602 L 79.201 1.602 C 80.51 1.602 81.671 0.974 82.401 0.002 L 82.046 0.18 C 81.49 0.458 80.877 0.602 80.256 0.602 L 2.149 0.602 C 1.529 0.602 0.919 0.458 0.364 0.182 L 0 0 C 0.755 1.009 1.941 1.603 3.201 1.602 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={40.500} height={7} viewBox="0 0 40.500 7" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 40.5,
        height: 7,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 35.697 4.803 L 40.5 0 L 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 7 L 30.393 7 C 32.382 7 34.29 6.21 35.697 4.803 L 35.697 4.803 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 256,
      position: "relative",
      ...props.style,
    }}>
      <svg width={224} height={160} viewBox="0 0 224 160" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 48,
        width: 224,
        height: 160,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 224 26 L 224 150 C 224 155.523 219.523 160 214 160 L 10 160 C 4.477 160 0 155.523 0 150 L 0 10 C 0 4.477 4.477 0 10 0 L 79.716 0 C 85.02 0 90.107 2.107 93.858 5.858 L 104 16 L 214 16 C 219.523 16 224 20.477 224 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <span style={{
        position: "absolute",
        left: 32,
        top: 159,
        width: 93,
        height: 33,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 29,
        whiteSpace: "nowrap",
        lineHeight: "100%",
        color: "rgb(191,87,18)",
      }}>{props.text1 ?? "0"}</span>
      <svg width={219.984} height={4.008} viewBox="0 0 219.984 4.008" fill="none" style={{
        position: "absolute",
        left: 18.008,
        top: 203.992,
        width: 219.984,
        height: 4.008,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 213.992 2.008 L 5.992 2.008 C 3.829 2.008 1.726 1.303 0 0 C 1.824 2.43 4.72 4.008 7.992 4.008 L 211.992 4.008 C 215.264 4.008 218.16 2.429 219.984 0 C 218.258 1.303 216.155 2.008 213.992 2.008 L 213.992 2.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={107} height={18} viewBox="0 0 107 18" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 107,
        height: 18,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 L 0 18 L 80.302 18 C 85.872 18 91.212 15.788 95.151 11.85 L 107 0 L 104 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={84} height={60} viewBox="0 0 84 60" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 18,
        width: 84,
        height: 60,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 39 6 L 35.05 2.05 C 33.737 0.737 31.957 0 30.1 0 L 4 0 C 1.791 0 0 1.791 0 4 L 0 56 C 0 58.209 1.791 60 4 60 L 80 60 C 82.209 60 84 58.209 84 56 L 84 10 C 84 7.791 82.209 6 80 6 L 39 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={82.401} height={1.602} viewBox="0 0 82.401 1.602" fill="none" style={{
        position: "absolute",
        left: 6.799,
        top: 76.398,
        width: 82.401,
        height: 1.602,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 3.201 1.602 L 79.201 1.602 C 80.51 1.602 81.671 0.974 82.401 0.002 L 82.046 0.18 C 81.49 0.458 80.877 0.602 80.256 0.602 L 2.149 0.602 C 1.529 0.602 0.919 0.458 0.364 0.182 L 0 0 C 0.755 1.009 1.941 1.603 3.201 1.602 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={40.500} height={7} viewBox="0 0 40.500 7" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 40.5,
        height: 7,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 35.697 4.803 L 40.5 0 L 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 7 L 30.393 7 C 32.382 7 34.29 6.21 35.697 4.803 L 35.697 4.803 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={42} height={30} viewBox="0 0 42 30" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 9,
        width: 42,
        height: 30,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 19 3 L 17.172 1.172 C 16.422 0.422 15.404 0 14.343 0 L 2.5 0 C 1.119 0 0 1.119 0 2.5 L 0 27.5 C 0 28.881 1.119 30 2.5 30 L 39.5 30 C 40.881 30 42 28.881 42 27.5 L 42 5.5 C 42 4.119 40.881 3 39.5 3 L 19 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={42} height={27} viewBox="0 0 42 27" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 12,
        width: 42,
        height: 27,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 19 0 L 17.025 1.975 C 16.369 2.631 15.478 3 14.55 3 L 0 3 L 0 24.5 C 0 25.881 1.119 27 2.5 27 L 39.5 27 C 40.881 27 42 25.881 42 24.5 L 42 2.5 C 42 1.119 40.881 0 39.5 0 L 19 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={42} height={27} viewBox="0 0 42 27" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 12,
        width: 42,
        height: 27,
      }}>
        <path d={"M 19 0 L 17.172 1.828 C 16.422 2.578 15.404 3 14.343 3 L 0 3 L 0 24.5 C 0 25.881 1.119 27 2.5 27 L 39.5 27 C 40.881 27 42 25.881 42 24.5 L 42 2.5 C 42 1.119 40.881 0 39.5 0 L 19 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={20.500} height={4} viewBox="0 0 20.500 4" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 12,
        width: 20.5,
        height: 4,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 19 0 L 17.172 1.828 C 16.422 2.578 15.404 3 14.343 3 L 0 3 L 0 4 L 14.636 4 C 15.829 4 16.974 3.526 17.818 2.682 L 20.5 0 L 19 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={41.680} height={1.647} viewBox="0 0 41.680 1.647" fill="none" style={{
        position: "absolute",
        left: 3.16,
        top: 37.353,
        width: 41.68,
        height: 1.647,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 1.84 0.647 C 1.171 0.645 0.523 0.417 0 0 C 0.356 0.987 1.291 1.645 2.34 1.647 L 39.34 1.647 C 40.42 1.647 41.331 0.958 41.68 0 C 41.171 0.399 40.538 0.647 39.84 0.647 L 1.84 0.647 L 1.84 0.647 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={26} viewBox="0 0 36 26" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 7,
        width: 36,
        height: 26,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 17 3 L 14.88 0.88 C 14.318 0.317 13.555 0.001 12.76 0 L 2 0 C 1.47 0 0.961 0.211 0.586 0.586 C 0.211 0.961 0 1.47 0 2 L 0 24 C 0 25.105 0.895 26 2 26 L 34 26 C 35.105 26 36 25.105 36 24 L 36 5 C 36 3.895 35.105 3 34 3 L 17 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={36} height={23} viewBox="0 0 36 23" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 10,
        width: 36,
        height: 23,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 17 0 L 14.88 2.12 C 14.318 2.683 13.555 2.999 12.76 3 L 0 3 L 0 21 C 0 22.105 0.895 23 2 23 L 34 23 C 35.105 23 36 22.105 36 21 L 36 2 C 36 0.895 35.105 0 34 0 L 17 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={36} height={23} viewBox="0 0 36 23" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 10,
        width: 36,
        height: 23,
        mixBlendMode: "multiply",
      }}>
        <path d={"M 17 0 L 14.88 2.12 C 14.318 2.683 13.555 2.999 12.76 3 L 0 3 L 0 21 C 0 22.105 0.895 23 2 23 L 34 23 C 35.105 23 36 22.105 36 21 L 36 2 C 36 0.895 35.105 0 34 0 L 17 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={18.530} height={4.000} viewBox="0 0 18.530 4.000" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 10,
        width: 18.53,
        height: 4,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 14.88 2.12 C 14.318 2.683 13.555 2.999 12.76 3 L 0 3 L 0 4 L 13.05 4 C 13.976 4.008 14.868 3.648 15.53 3 L 18.53 0 L 17 0 L 14.88 2.12 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={35.840} height={1.430} viewBox="0 0 35.840 1.430" fill="none" style={{
        position: "absolute",
        left: 2.08,
        top: 31.57,
        width: 35.84,
        height: 1.43,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 1.92 1.43 L 33.92 1.43 C 34.806 1.431 35.587 0.849 35.84 0 C 35.489 0.277 35.057 0.428 34.61 0.43 L 1.24 0.43 C 0.79 0.43 0.353 0.279 0 0 C 0.253 0.849 1.034 1.431 1.92 1.43 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={20} viewBox="0 0 28 20" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 6,
        width: 28,
        height: 20,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 13 2 L 11.732 0.732 C 11.263 0.263 10.627 0 9.964 0 L 1.5 0 C 0.672 0 0 0.672 0 1.5 L 0 18.5 C 0 19.328 0.672 20 1.5 20 L 26.5 20 C 27.328 20 28 19.328 28 18.5 L 28 3.5 C 28 2.672 27.328 2 26.5 2 L 13 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={28} height={18} viewBox="0 0 28 18" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 8,
        width: 28,
        height: 18,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 13 0 L 11.732 1.268 C 11.263 1.737 10.627 2 9.964 2 L 0 2 L 0 16.5 C 0 17.328 0.672 18 1.5 18 L 26.5 18 C 27.328 18 28 17.328 28 16.5 L 28 1.5 C 28 0.672 27.328 0 26.5 0 L 13 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={28} height={18} viewBox="0 0 28 18" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 8,
        width: 28,
        height: 18,
      }}>
        <path d={"M 13 0 L 11.732 1.268 C 11.263 1.737 10.627 2 9.964 2 L 0 2 L 0 16.5 C 0 17.328 0.672 18 1.5 18 L 26.5 18 C 27.328 18 28 17.328 28 16.5 L 28 1.5 C 28 0.672 27.328 0 26.5 0 L 13 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={27.554} height={1} viewBox="0 0 27.554 1" fill="none" style={{
        position: "absolute",
        left: 2.223,
        top: 25.139,
        width: 27.554,
        height: 1,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 0.777 0.361 C 0.491 0.361 0.227 0.278 0 0.139 C 0.263 0.571 0.734 0.862 1.277 0.862 L 26.277 0.862 C 26.82 0.862 27.291 0.571 27.554 0.139 C 27.321 0.283 27.052 0.36 26.777 0.361 L 0.777 0.361 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={13.750} height={2.500} viewBox="0 0 13.750 2.500" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 8,
        width: 13.75,
        height: 2.5,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 12.129 1.621 L 13.75 0 L 13 0 L 11.732 1.268 C 11.263 1.737 10.627 2 9.964 2 L 0 2 L 0 2.5 L 10.007 2.5 C 10.803 2.5 11.566 2.184 12.129 1.621 L 12.129 1.621 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={22} height={16} viewBox="0 0 22 16" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 4,
        width: 22,
        height: 16,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 11 2 L 9.732 0.732 C 9.263 0.263 8.627 0 7.964 0 L 1.5 0 C 0.672 0 0 0.672 0 1.5 L 0 14.5 C 0 15.328 0.672 16 1.5 16 L 20.5 16 C 21.328 16 22 15.328 22 14.5 L 22 3.5 C 22 2.672 21.328 2 20.5 2 L 11 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={22} height={14} viewBox="0 0 22 14" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 6,
        width: 22,
        height: 14,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 11 0 L 9.732 1.268 C 9.263 1.737 8.627 2 7.964 2 L 0 2 L 0 12.5 C 0 13.328 0.672 14 1.5 14 L 20.5 14 C 21.328 14 22 13.328 22 12.5 L 22 1.5 C 22 0.672 21.328 0 20.5 0 L 11 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={22} height={14} viewBox="0 0 22 14" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 6,
        width: 22,
        height: 14,
      }}>
        <path d={"M 11 0 L 9.732 1.268 C 9.263 1.737 8.627 2 7.964 2 L 0 2 L 0 12.5 C 0 13.328 0.672 14 1.5 14 L 20.5 14 C 21.328 14 22 13.328 22 12.5 L 22 1.5 C 22 0.672 21.328 0 20.5 0 L 11 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={21.554} height={1} viewBox="0 0 21.554 1" fill="none" style={{
        position: "absolute",
        left: 1.223,
        top: 19.139,
        width: 21.554,
        height: 1,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 0.777 0.361 C 0.491 0.361 0.227 0.278 0 0.139 C 0.263 0.571 0.734 0.862 1.277 0.862 L 20.277 0.862 C 20.82 0.862 21.291 0.571 21.554 0.139 C 21.321 0.283 21.052 0.36 20.777 0.361 L 0.777 0.361 L 0.777 0.361 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={11.750} height={2.500} viewBox="0 0 11.750 2.500" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 6,
        width: 11.75,
        height: 2.5,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 10.129 1.621 L 11.75 0 L 11 0 L 9.732 1.268 C 9.263 1.737 8.627 2 7.964 2 L 0 2 L 0 2.5 L 8.007 2.5 C 8.803 2.5 9.566 2.184 10.129 1.621 L 10.129 1.621 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={18} height={14} viewBox="0 0 18 14" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 3,
        width: 18,
        height: 14,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 9 2 L 7.586 0.586 C 7.211 0.211 6.702 0 6.172 0 L 1 0 C 0.448 0 0 0.448 0 1 L 0 13 C 0 13.552 0.448 14 1 14 L 17 14 C 17.552 14 18 13.552 18 13 L 18 3 C 18 2.448 17.552 2 17 2 L 9 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={18} height={12} viewBox="0 0 18 12" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 5,
        width: 18,
        height: 12,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 9 0 L 7.586 1.414 C 7.211 1.789 6.702 2 6.172 2 L 0 2 L 0 11 C 0 11.552 0.448 12 1 12 L 17 12 C 17.552 12 18 11.552 18 11 L 18 1 C 18 0.448 17.552 0 17 0 L 9 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={18} height={12} viewBox="0 0 18 12" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 5,
        width: 18,
        height: 12,
      }}>
        <path d={"M 9 0 L 7.586 1.414 C 7.211 1.789 6.702 2 6.172 2 L 0 2 L 0 11 C 0 11.552 0.448 12 1 12 L 17 12 C 17.552 12 18 11.552 18 11 L 18 1 C 18 0.448 17.552 0 17 0 L 9 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={17.946} height={1} viewBox="0 0 17.946 1" fill="none" style={{
        position: "absolute",
        left: 1.027,
        top: 16.066,
        width: 17.946,
        height: 1,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 0.973 0.433 C 0.6 0.433 0.263 0.292 0 0.067 C 0.061 0.559 0.477 0.93 0.973 0.933 L 16.973 0.933 C 17.478 0.933 17.878 0.553 17.946 0.067 C 17.684 0.292 17.346 0.433 16.973 0.433 L 0.973 0.433 L 0.973 0.433 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={9.750} height={2.500} viewBox="0 0 9.750 2.500" fill="none" style={{
        position: "absolute",
        left: 1,
        top: 5,
        width: 9.75,
        height: 2.5,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 7.836 1.914 L 9.75 0 L 9 0 L 7.586 1.414 C 7.211 1.789 6.702 2 6.172 2 L 0 2 L 0 2.5 L 6.422 2.5 C 6.952 2.5 7.461 2.289 7.836 1.914 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={12} viewBox="0 0 16 12" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 2,
        width: 16,
        height: 12,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 8 2 L 6.586 0.586 C 6.211 0.211 5.702 0 5.172 0 L 1 0 C 0.448 0 0 0.448 0 1 L 0 11 C 0 11.552 0.448 12 1 12 L 15 12 C 15.552 12 16 11.552 16 11 L 16 3 C 16 2.448 15.552 2 15 2 L 8 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={10} viewBox="0 0 16 10" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 16,
        height: 10,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 8 0 L 6.586 1.414 C 6.211 1.789 5.702 2 5.172 2 L 0 2 L 0 9 C 0 9.552 0.448 10 1 10 L 15 10 C 15.552 10 16 9.552 16 9 L 16 1 C 16 0.448 15.552 0 15 0 L 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={10} viewBox="0 0 16 10" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 16,
        height: 10,
      }}>
        <path d={"M 8 0 L 6.586 1.414 C 6.211 1.789 5.702 2 5.172 2 L 0 2 L 0 9 C 0 9.552 0.448 10 1 10 L 15 10 C 15.552 10 16 9.552 16 9 L 16 1 C 16 0.448 15.552 0 15 0 L 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={15.946} height={1} viewBox="0 0 15.946 1" fill="none" style={{
        position: "absolute",
        left: 0.027,
        top: 13.066,
        width: 15.946,
        height: 1,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 0.973 0.433 C 0.6 0.433 0.263 0.292 0 0.067 C 0.061 0.559 0.477 0.93 0.973 0.933 L 14.973 0.933 C 15.478 0.933 15.878 0.553 15.946 0.067 C 15.684 0.292 15.346 0.433 14.973 0.433 L 0.973 0.433 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={8.750} height={2.500} viewBox="0 0 8.750 2.500" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 4,
        width: 8.75,
        height: 2.5,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 6.836 1.914 L 8.75 0 L 8 0 L 6.586 1.414 C 6.211 1.789 5.702 2 5.172 2 L 0 2 L 0 2.5 L 5.422 2.5 C 5.952 2.5 6.461 2.289 6.836 1.914 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 256,
      position: "relative",
      ...props.style,
    }}>
      <svg width={224} height={160} viewBox="0 0 224 160" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 48,
        width: 224,
        height: 160,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 224 26 L 224 150 C 224 155.523 219.523 160 214 160 L 10 160 C 4.477 160 0 155.523 0 150 L 0 10 C 0 4.477 4.477 0 10 0 L 79.716 0 C 85.02 0 90.107 2.107 93.858 5.858 L 104 16 L 214 16 C 219.523 16 224 20.477 224 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 24,
        top: 72,
        width: 208,
        height: 128,
        borderRadius: 2,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <span style={{
        position: "absolute",
        left: 32,
        top: 159,
        width: 93,
        height: 33,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 29,
        whiteSpace: "nowrap",
        lineHeight: "100%",
        color: "rgb(191,87,18)",
      }}>{props.text1 ?? "10"}</span>
      <svg width={219.984} height={4.008} viewBox="0 0 219.984 4.008" fill="none" style={{
        position: "absolute",
        left: 18.008,
        top: 203.992,
        width: 219.984,
        height: 4.008,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 213.992 2.008 L 5.992 2.008 C 3.829 2.008 1.726 1.303 0 0 C 1.824 2.43 4.72 4.008 7.992 4.008 L 211.992 4.008 C 215.264 4.008 218.16 2.429 219.984 0 C 218.258 1.303 216.155 2.008 213.992 2.008 L 213.992 2.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={107} height={18} viewBox="0 0 107 18" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 107,
        height: 18,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 L 0 18 L 80.302 18 C 85.872 18 91.212 15.788 95.151 11.85 L 107 0 L 104 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 256,
      position: "relative",
      ...props.style,
    }}>
      <svg width={224} height={160} viewBox="0 0 224 160" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 48,
        width: 224,
        height: 160,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 224 26 L 224 150 C 224 155.523 219.523 160 214 160 L 10 160 C 4.477 160 0 155.523 0 150 L 0 10 C 0 4.477 4.477 0 10 0 L 79.716 0 C 85.02 0 90.107 2.107 93.858 5.858 L 104 16 L 214 16 C 219.523 16 224 20.477 224 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={219.984} height={4.008} viewBox="0 0 219.984 4.008" fill="none" style={{
        position: "absolute",
        left: 18.008,
        top: 203.992,
        width: 219.984,
        height: 4.008,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 213.992 2.008 L 5.992 2.008 C 3.829 2.008 1.726 1.303 0 0 C 1.824 2.43 4.72 4.008 7.992 4.008 L 211.992 4.008 C 215.264 4.008 218.16 2.429 219.984 0 C 218.258 1.303 216.155 2.008 213.992 2.008 L 213.992 2.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={107} height={18} viewBox="0 0 107 18" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 107,
        height: 18,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 L 0 18 L 80.302 18 C 85.872 18 91.212 15.788 95.151 11.85 L 107 0 L 104 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 256,
      position: "relative",
      ...props.style,
    }}>
      <svg width={224} height={160} viewBox="0 0 224 160" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 48,
        width: 224,
        height: 160,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 224 26 L 224 150 C 224 155.523 219.523 160 214 160 L 10 160 C 4.477 160 0 155.523 0 150 L 0 10 C 0 4.477 4.477 0 10 0 L 79.716 0 C 85.02 0 90.107 2.107 93.858 5.858 L 104 16 L 214 16 C 219.523 16 224 20.477 224 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 24,
        top: 72,
        width: 208,
        height: 128,
        borderRadius: 2,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={144} viewBox="0 0 224 144" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 224,
        height: 144,
      }}>
        <path d={"M 0 16 L 0 134 C 0 139.523 4.477 144 10 144 L 214 144 C 219.523 144 224 139.523 224 134 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={219.984} height={4.008} viewBox="0 0 219.984 4.008" fill="none" style={{
        position: "absolute",
        left: 18.008,
        top: 203.992,
        width: 219.984,
        height: 4.008,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 213.992 2.008 L 5.992 2.008 C 3.829 2.008 1.726 1.303 0 0 C 1.824 2.43 4.72 4.008 7.992 4.008 L 211.992 4.008 C 215.264 4.008 218.16 2.429 219.984 0 C 218.258 1.303 216.155 2.008 213.992 2.008 L 213.992 2.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={107} height={18} viewBox="0 0 107 18" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 64,
        width: 107,
        height: 18,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 L 0 18 L 80.302 18 C 85.872 18 91.212 15.788 95.151 11.85 L 107 0 L 104 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={84} height={60} viewBox="0 0 84 60" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 18,
        width: 84,
        height: 60,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 39 6 L 35.05 2.05 C 33.737 0.737 31.957 0 30.1 0 L 4 0 C 1.791 0 0 1.791 0 4 L 0 56 C 0 58.209 1.791 60 4 60 L 80 60 C 82.209 60 84 58.209 84 56 L 84 10 C 84 7.791 82.209 6 80 6 L 39 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 9,
        top: 27,
        width: 78,
        height: 49,
        borderRadius: 1,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={82.401} height={1.602} viewBox="0 0 82.401 1.602" fill="none" style={{
        position: "absolute",
        left: 6.799,
        top: 76.398,
        width: 82.401,
        height: 1.602,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 3.201 1.602 L 79.201 1.602 C 80.51 1.602 81.671 0.974 82.401 0.002 L 82.046 0.18 C 81.49 0.458 80.877 0.602 80.256 0.602 L 2.149 0.602 C 1.529 0.602 0.919 0.458 0.364 0.182 L 0 0 C 0.755 1.009 1.941 1.603 3.201 1.602 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={40.500} height={7} viewBox="0 0 40.500 7" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 40.5,
        height: 7,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 35.697 4.803 L 40.5 0 L 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 7 L 30.393 7 C 32.382 7 34.29 6.21 35.697 4.803 L 35.697 4.803 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={84} height={60} viewBox="0 0 84 60" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 18,
        width: 84,
        height: 60,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 39 6 L 35.05 2.05 C 33.737 0.737 31.957 0 30.1 0 L 4 0 C 1.791 0 0 1.791 0 4 L 0 56 C 0 58.209 1.791 60 4 60 L 80 60 C 82.209 60 84 58.209 84 56 L 84 10 C 84 7.791 82.209 6 80 6 L 39 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 9,
        top: 27,
        width: 78,
        height: 49,
        borderRadius: 1,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={54} viewBox="0 0 84 54" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 84,
        height: 54,
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 50 C 0 52.209 1.791 54 4 54 L 80 54 C 82.209 54 84 52.209 84 50 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <span style={{
        position: "absolute",
        left: 12,
        top: 58,
        width: 50,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "100%",
        color: "rgb(191,87,18)",
      }}>{props.text1 ?? "10"}</span>
      <svg width={82.401} height={1.602} viewBox="0 0 82.401 1.602" fill="none" style={{
        position: "absolute",
        left: 6.799,
        top: 76.398,
        width: 82.401,
        height: 1.602,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 3.201 1.602 L 79.201 1.602 C 80.51 1.602 81.671 0.974 82.401 0.002 L 82.046 0.18 C 81.49 0.458 80.877 0.602 80.256 0.602 L 2.149 0.602 C 1.529 0.602 0.919 0.458 0.364 0.182 L 0 0 C 0.755 1.009 1.941 1.603 3.201 1.602 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={40.500} height={7} viewBox="0 0 40.500 7" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 24,
        width: 40.5,
        height: 7,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 35.697 4.803 L 40.5 0 L 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 7 L 30.393 7 C 32.382 7 34.29 6.21 35.697 4.803 L 35.697 4.803 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={42} height={30} viewBox="0 0 42 30" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 9,
        width: 42,
        height: 30,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 19 3 L 17.172 1.172 C 16.422 0.422 15.404 0 14.343 0 L 2.5 0 C 1.119 0 0 1.119 0 2.5 L 0 27.5 C 0 28.881 1.119 30 2.5 30 L 39.5 30 C 40.881 30 42 28.881 42 27.5 L 42 5.5 C 42 4.119 40.881 3 39.5 3 L 19 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 5,
        top: 13,
        width: 38,
        height: 24,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 38,
          height: 24,
          borderRadius: 0.5,
          backgroundColor: "rgb(255,255,255)",
        }} />
      </div>
      <svg width={42} height={27} viewBox="0 0 42 27" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 12,
        width: 42,
        height: 27,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 19 0 L 17.025 1.975 C 16.369 2.631 15.478 3 14.55 3 L 0 3 L 0 24.5 C 0 25.881 1.119 27 2.5 27 L 39.5 27 C 40.881 27 42 25.881 42 24.5 L 42 2.5 C 42 1.119 40.881 0 39.5 0 L 19 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={42} height={27} viewBox="0 0 42 27" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 12,
        width: 42,
        height: 27,
      }}>
        <path d={"M 19 0 L 17.172 1.828 C 16.422 2.578 15.404 3 14.343 3 L 0 3 L 0 24.5 C 0 25.881 1.119 27 2.5 27 L 39.5 27 C 40.881 27 42 25.881 42 24.5 L 42 2.5 C 42 1.119 40.881 0 39.5 0 L 19 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={20.500} height={4} viewBox="0 0 20.500 4" fill="none" style={{
        position: "absolute",
        left: 3,
        top: 12,
        width: 20.5,
        height: 4,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 19 0 L 17.172 1.828 C 16.422 2.578 15.404 3 14.343 3 L 0 3 L 0 4 L 14.636 4 C 15.829 4 16.974 3.526 17.818 2.682 L 20.5 0 L 19 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={41.680} height={1.647} viewBox="0 0 41.680 1.647" fill="none" style={{
        position: "absolute",
        left: 3.16,
        top: 37.353,
        width: 41.68,
        height: 1.647,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 1.84 0.647 C 1.171 0.645 0.523 0.417 0 0 C 0.356 0.987 1.291 1.645 2.34 1.647 L 39.34 1.647 C 40.42 1.647 41.331 0.958 41.68 0 C 41.171 0.399 40.538 0.647 39.84 0.647 L 1.84 0.647 L 1.84 0.647 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={26} viewBox="0 0 36 26" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 7,
        width: 36,
        height: 26,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 17 3 L 14.88 0.88 C 14.318 0.317 13.555 0.001 12.76 0 L 2 0 C 1.47 0 0.961 0.211 0.586 0.586 C 0.211 0.961 0 1.47 0 2 L 0 24 C 0 25.105 0.895 26 2 26 L 34 26 C 35.105 26 36 25.105 36 24 L 36 5 C 36 3.895 35.105 3 34 3 L 17 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 4,
        top: 11,
        width: 32,
        height: 19,
        borderRadius: 0.5,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <svg width={36} height={23} viewBox="0 0 36 23" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 10,
        width: 36,
        height: 23,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 17 0 L 14.88 2.12 C 14.318 2.683 13.555 2.999 12.76 3 L 0 3 L 0 21 C 0 22.105 0.895 23 2 23 L 34 23 C 35.105 23 36 22.105 36 21 L 36 2 C 36 0.895 35.105 0 34 0 L 17 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={36} height={23} viewBox="0 0 36 23" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 10,
        width: 36,
        height: 23,
        mixBlendMode: "multiply",
      }}>
        <path d={"M 17 0 L 14.88 2.12 C 14.318 2.683 13.555 2.999 12.76 3 L 0 3 L 0 21 C 0 22.105 0.895 23 2 23 L 34 23 C 35.105 23 36 22.105 36 21 L 36 2 C 36 0.895 35.105 0 34 0 L 17 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={18.530} height={4.000} viewBox="0 0 18.530 4.000" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 10,
        width: 18.53,
        height: 4,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 14.88 2.12 C 14.318 2.683 13.555 2.999 12.76 3 L 0 3 L 0 4 L 13.05 4 C 13.976 4.008 14.868 3.648 15.53 3 L 18.53 0 L 17 0 L 14.88 2.12 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={35.840} height={1.430} viewBox="0 0 35.840 1.430" fill="none" style={{
        position: "absolute",
        left: 2.08,
        top: 31.57,
        width: 35.84,
        height: 1.43,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 1.92 1.43 L 33.92 1.43 C 34.806 1.431 35.587 0.849 35.84 0 C 35.489 0.277 35.057 0.428 34.61 0.43 L 1.24 0.43 C 0.79 0.43 0.353 0.279 0 0 C 0.253 0.849 1.034 1.431 1.92 1.43 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 256,
      position: "relative",
      ...props.style,
    }}>
      <svg width={224} height={160} viewBox="0 0 224 160" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 48,
        width: 224,
        height: 160,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 224 26 L 224 150 C 224 155.523 219.523 160 214 160 L 10 160 C 4.477 160 0 155.523 0 150 L 0 10 C 0 4.477 4.477 0 10 0 L 79.716 0 C 85.02 0 90.107 2.107 93.858 5.858 L 104 16 L 214 16 C 219.523 16 224 20.477 224 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 24,
        top: 72,
        width: 208,
        height: 128,
        borderRadius: 2,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <div className="fig-asset-80a3950c68c5fde7" style={{
        position: "absolute",
        left: 32,
        top: 80,
        width: 192,
        height: 112,
      }} />
      <svg width={224} height={67} viewBox="0 0 224 67" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 141,
        width: 224,
        height: 67,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 0 16 L 0 57 C 0 62.523 4.477 67 10 67 L 214 67 C 219.523 67 224 62.523 224 57 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={67} viewBox="0 0 224 67" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 141,
        width: 224,
        height: 67,
      }}>
        <path d={"M 0 16 L 0 57 C 0 62.523 4.477 67 10 67 L 214 67 C 219.523 67 224 62.523 224 57 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <span style={{
        position: "absolute",
        left: 32,
        top: 159,
        width: 93,
        height: 33,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 29,
        whiteSpace: "nowrap",
        lineHeight: "100%",
        color: "rgb(191,87,18)",
      }}>{props.text1 ?? "10"}</span>
      <svg width={219.984} height={4.008} viewBox="0 0 219.984 4.008" fill="none" style={{
        position: "absolute",
        left: 18.008,
        top: 203.992,
        width: 219.984,
        height: 4.008,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 213.992 2.008 L 5.992 2.008 C 3.829 2.008 1.726 1.303 0 0 C 1.824 2.43 4.72 4.008 7.992 4.008 L 211.992 4.008 C 215.264 4.008 218.16 2.429 219.984 0 C 218.258 1.303 216.155 2.008 213.992 2.008 L 213.992 2.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={107} height={18} viewBox="0 0 107 18" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 141,
        width: 107,
        height: 18,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 L 0 18 L 80.302 18 C 85.872 18 91.212 15.788 95.151 11.85 L 107 0 L 104 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={84} height={60} viewBox="0 0 84 60" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 18,
        width: 84,
        height: 60,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 39 6 L 35.05 2.05 C 33.737 0.737 31.957 0 30.1 0 L 4 0 C 1.791 0 0 1.791 0 4 L 0 56 C 0 58.209 1.791 60 4 60 L 80 60 C 82.209 60 84 58.209 84 56 L 84 10 C 84 7.791 82.209 6 80 6 L 39 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 9,
        top: 27,
        width: 78,
        height: 49,
        borderRadius: 1,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <div className="fig-asset-80a3950c68c5fde7" style={{
        position: "absolute",
        left: 11,
        top: 29,
        width: 74,
        height: 47,
      }} />
      <svg width={84} height={27} viewBox="0 0 84 27" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 51,
        width: 84,
        height: 27,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 23 C 0 25.209 1.791 27 4 27 L 80 27 C 82.209 27 84 25.209 84 23 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={27} viewBox="0 0 84 27" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 51,
        width: 84,
        height: 27,
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 23 C 0 25.209 1.791 27 4 27 L 80 27 C 82.209 27 84 25.209 84 23 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <span style={{
        position: "absolute",
        left: 12,
        top: 58,
        width: 50,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "100%",
        color: "rgb(191,87,18)",
      }}>{props.text1 ?? "10"}</span>
      <svg width={82.401} height={1.602} viewBox="0 0 82.401 1.602" fill="none" style={{
        position: "absolute",
        left: 6.799,
        top: 76.398,
        width: 82.401,
        height: 1.602,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 3.201 1.602 L 79.201 1.602 C 80.51 1.602 81.671 0.974 82.401 0.002 L 82.046 0.18 C 81.49 0.458 80.877 0.602 80.256 0.602 L 2.149 0.602 C 1.529 0.602 0.919 0.458 0.364 0.182 L 0 0 C 0.755 1.009 1.941 1.603 3.201 1.602 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={40.500} height={7} viewBox="0 0 40.500 7" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 51,
        width: 40.5,
        height: 7,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 35.697 4.803 L 40.5 0 L 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 7 L 30.393 7 C 32.382 7 34.29 6.21 35.697 4.803 L 35.697 4.803 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={84} height={60} viewBox="0 0 84 60" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 18,
        width: 84,
        height: 60,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 39 6 L 35.05 2.05 C 33.737 0.737 31.957 0 30.1 0 L 4 0 C 1.791 0 0 1.791 0 4 L 0 56 C 0 58.209 1.791 60 4 60 L 80 60 C 82.209 60 84 58.209 84 56 L 84 10 C 84 7.791 82.209 6 80 6 L 39 6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 9,
        top: 27,
        width: 78,
        height: 49,
        borderRadius: 1,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <div className="fig-asset-80a3950c68c5fde7" style={{
        position: "absolute",
        left: 11,
        top: 29,
        width: 74,
        height: 47,
      }} />
      <svg width={84} height={27} viewBox="0 0 84 27" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 51,
        width: 84,
        height: 27,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 23 C 0 25.209 1.791 27 4 27 L 80 27 C 82.209 27 84 25.209 84 23 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={84} height={27} viewBox="0 0 84 27" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 51,
        width: 84,
        height: 27,
      }}>
        <path d={"M 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 23 C 0 25.209 1.791 27 4 27 L 80 27 C 82.209 27 84 25.209 84 23 L 84 4 C 84 1.791 82.209 0 80 0 L 39 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={82.401} height={1.602} viewBox="0 0 82.401 1.602" fill="none" style={{
        position: "absolute",
        left: 6.799,
        top: 76.398,
        width: 82.401,
        height: 1.602,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 3.201 1.602 L 79.201 1.602 C 80.51 1.602 81.671 0.974 82.401 0.002 L 82.046 0.18 C 81.49 0.458 80.877 0.602 80.256 0.602 L 2.149 0.602 C 1.529 0.602 0.919 0.458 0.364 0.182 L 0 0 C 0.755 1.009 1.941 1.603 3.201 1.602 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={40.500} height={7} viewBox="0 0 40.500 7" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 51,
        width: 40.5,
        height: 7,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 35.697 4.803 L 40.5 0 L 39 0 L 35.05 3.95 C 33.737 5.263 31.957 6 30.1 6 L 0 6 L 0 7 L 30.393 7 C 32.382 7 34.29 6.21 35.697 4.803 L 35.697 4.803 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 256,
      height: 256,
      position: "relative",
      ...props.style,
    }}>
      <svg width={224} height={160} viewBox="0 0 224 160" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 48,
        width: 224,
        height: 160,
        color: "rgb(255,185,0)",
      }}>
        <path d={"M 224 26 L 224 150 C 224 155.523 219.523 160 214 160 L 10 160 C 4.477 160 0 155.523 0 150 L 0 10 C 0 4.477 4.477 0 10 0 L 79.716 0 C 85.02 0 90.107 2.107 93.858 5.858 L 104 16 L 214 16 C 219.523 16 224 20.477 224 26 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 24,
        top: 72,
        width: 208,
        height: 128,
        borderRadius: 2,
        backgroundColor: "rgb(255,255,255)",
      }} />
      <div className="fig-asset-80a3950c68c5fde7" style={{
        position: "absolute",
        left: 32,
        top: 80,
        width: 192,
        height: 112,
      }} />
      <svg width={224} height={67} viewBox="0 0 224 67" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 141,
        width: 224,
        height: 67,
        color: "rgb(255,215,94)",
      }}>
        <path d={"M 0 16 L 0 57 C 0 62.523 4.477 67 10 67 L 214 67 C 219.523 67 224 62.523 224 57 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={224} height={67} viewBox="0 0 224 67" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 141,
        width: 224,
        height: 67,
      }}>
        <path d={"M 0 16 L 0 57 C 0 62.523 4.477 67 10 67 L 214 67 C 219.523 67 224 62.523 224 57 L 224 10 C 224 4.477 219.523 0 214 0 L 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={219.984} height={4.008} viewBox="0 0 219.984 4.008" fill="none" style={{
        position: "absolute",
        left: 18.008,
        top: 203.992,
        width: 219.984,
        height: 4.008,
        color: "rgb(191,87,18)",
      }}>
        <path d={"M 213.992 2.008 L 5.992 2.008 C 3.829 2.008 1.726 1.303 0 0 C 1.824 2.43 4.72 4.008 7.992 4.008 L 211.992 4.008 C 215.264 4.008 218.16 2.429 219.984 0 C 218.258 1.303 216.155 2.008 213.992 2.008 L 213.992 2.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={107} height={18} viewBox="0 0 107 18" fill="none" style={{
        position: "absolute",
        left: 16,
        top: 141,
        width: 107,
        height: 18,
        opacity: 0.4,
        color: "rgb(255,255,255)",
      }}>
        <path d={"M 104 0 L 93.858 10.142 C 90.107 13.893 85.02 16 79.716 16 L 0 16 L 0 18 L 80.302 18 C 85.872 18 91.212 15.788 95.151 11.85 L 107 0 L 104 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 2,
        top: 6,
        width: 32,
        height: 24,
        overflow: "hidden",
      }}>
        <svg width={32} height={24} viewBox="0 0 32 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 24,
          color: "rgb(255,185,0)",
        }}>
          <path d={"M 15 3 L 12.879 0.879 C 12.316 0.316 11.553 0 10.757 0 L 2 0 C 0.895 0 0 0.895 0 2 L 0 22 C 0 23.105 0.895 24 2 24 L 30 24 C 31.105 24 32 23.105 32 22 L 32 5 C 32 3.895 31.105 3 30 3 L 15 3 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <div style={{
          position: "absolute",
          left: 2,
          top: 5,
          width: 28,
          height: 17,
          borderRadius: 0.5,
          backgroundColor: "rgb(255,255,255)",
        }} />
        <svg width={32} height={21} viewBox="0 0 32 21" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 32,
          height: 21,
          color: "rgb(255,215,94)",
        }}>
          <path d={"M 15 0 L 12.879 2.121 C 12.316 2.684 11.553 3 10.757 3 L 0 3 L 0 19 C 0 20.105 0.895 21 2 21 L 30 21 C 31.105 21 32 20.105 32 19 L 32 2 C 32 0.895 31.105 0 30 0 L 15 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={32} height={21} viewBox="0 0 32 21" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 32,
          height: 21,
        }}>
          <path d={"M 15 0 L 12.879 2.121 C 12.316 2.684 11.553 3 10.757 3 L 0 3 L 0 19 C 0 20.105 0.895 21 2 21 L 30 21 C 31.105 21 32 20.105 32 19 L 32 2 C 32 0.895 31.105 0 30 0 L 15 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={16.500} height={4} viewBox="0 0 16.500 4" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 16.5,
          height: 4,
          opacity: 0.4,
          color: "rgb(255,255,255)",
        }}>
          <path d={"M 12.879 2.121 C 12.316 2.684 11.553 3 10.757 3 L 0 3 L 0 4 L 11.05 4 C 11.978 4 12.869 3.631 13.525 2.975 L 16.5 0 L 15 0 L 12.879 2.121 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={31.836} height={1.430} viewBox="0 0 31.836 1.430" fill="none" style={{
          position: "absolute",
          left: 0.082,
          top: 22.57,
          width: 31.836,
          height: 1.43,
          color: "rgb(191,87,18)",
        }}>
          <path d={"M 1.918 1.43 L 29.918 1.43 C 30.803 1.43 31.584 0.849 31.836 0 C 31.486 0.277 31.052 0.43 30.603 0.43 L 1.233 0.43 C 0.785 0.43 0.351 0.279 0 0 C 0.252 0.849 1.033 1.43 1.918 1.43 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 2,
        top: 6,
        width: 32,
        height: 24,
        overflow: "hidden",
      }}>
        <svg width={32} height={24} viewBox="0 0 32 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 24,
          color: "rgb(255,185,0)",
        }}>
          <path d={"M 15 3 L 12.879 0.879 C 12.316 0.316 11.553 0 10.757 0 L 2 0 C 0.895 0 0 0.895 0 2 L 0 22 C 0 23.105 0.895 24 2 24 L 30 24 C 31.105 24 32 23.105 32 22 L 32 5 C 32 3.895 31.105 3 30 3 L 15 3 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={32} height={21} viewBox="0 0 32 21" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 32,
          height: 21,
          color: "rgb(255,215,94)",
        }}>
          <path d={"M 15 0 L 12.879 2.121 C 12.316 2.684 11.553 3 10.757 3 L 0 3 L 0 19 C 0 20.105 0.895 21 2 21 L 30 21 C 31.105 21 32 20.105 32 19 L 32 2 C 32 0.895 31.105 0 30 0 L 15 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={32} height={21} viewBox="0 0 32 21" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 32,
          height: 21,
        }}>
          <path d={"M 15 0 L 12.879 2.121 C 12.316 2.684 11.553 3 10.757 3 L 0 3 L 0 19 C 0 20.105 0.895 21 2 21 L 30 21 C 31.105 21 32 20.105 32 19 L 32 2 C 32 0.895 31.105 0 30 0 L 15 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={16.500} height={4} viewBox="0 0 16.500 4" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 3,
          width: 16.5,
          height: 4,
          opacity: 0.4,
          color: "rgb(255,255,255)",
        }}>
          <path d={"M 12.879 2.121 C 12.316 2.684 11.553 3 10.757 3 L 0 3 L 0 4 L 11.05 4 C 11.978 4 12.869 3.631 13.525 2.975 L 16.5 0 L 15 0 L 12.879 2.121 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
        <svg width={31.836} height={1.430} viewBox="0 0 31.836 1.430" fill="none" style={{
          position: "absolute",
          left: 0.082,
          top: 22.57,
          width: 31.836,
          height: 1.43,
          color: "rgb(191,87,18)",
        }}>
          <path d={"M 1.918 1.43 L 29.918 1.43 C 30.803 1.43 31.584 0.849 31.836 0 C 31.486 0.277 31.052 0.43 30.603 0.43 L 1.233 0.43 C 0.785 0.43 0.351 0.279 0 0 C 0.252 0.849 1.033 1.43 1.918 1.43 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=96, Item Count=Yes, Preview=None
    "size=96|itemCount=true|preview=none": __body0,
    // figma: Size=256, Item Count = Yes, Preview=None
    "size=256|itemCount=true|preview=none": __body1,
    // figma: Size=96, Item Count=No, Preview=None
    "size=96|itemCount=false|preview=none": __body2,
    // figma: Size=48, Item Count = No, Preview=None
    "size=48|itemCount=false|preview=none": __body3,
    // figma: Size=40, Item Count = No, Preview=None
    "size=40|itemCount=false|preview=none": __body4,
    // figma: Size=32, Item Count = No, Preview=None
    "size=32|itemCount=false|preview=none": __body5,
    // figma: Size=24, Item Count = No, Preview=None
    "size=24|itemCount=false|preview=none": __body6,
    // figma: Size=20, Item Count = No, Preview=None
    "size=20|itemCount=false|preview=none": __body7,
    // figma: Size=16, Item Count = No, Preview=None
    "size=16|itemCount=false|preview=none": __body8,
    // figma: Size=256, Item Count = Yes, Preview=Filled
    "size=256|itemCount=true|preview=filled": __body9,
    // figma: Size=256, Item Count = No, Preview=None
    "size=256|itemCount=false|preview=none": __body10,
    // figma: Size=256, Item Count = No, Preview=Filled
    "size=256|itemCount=false|preview=filled": __body11,
    // figma: Size=96, Item Count = No, Preview=Filled
    "size=96|itemCount=false|preview=filled": __body12,
    // figma: Size=96, Item Count = Yes, Preview=Filled
    "size=96|itemCount=true|preview=filled": __body13,
    // figma: Size=48, Item Count = No, Preview=Filled
    "size=48|itemCount=false|preview=filled": __body14,
    // figma: Size=40, Item Count = No, Preview=Filled
    "size=40|itemCount=false|preview=filled": __body15,
    // figma: Size=256, Item Count = Yes, Preview=Thumbnail
    "size=256|itemCount=true|preview=thumbnail": __body16,
    // figma: Size=96, Item Count = Yes, Preview=Thumbnail
    "size=96|itemCount=true|preview=thumbnail": __body17,
    // figma: Size=96, Item Count = No, Preview=Thumbnail
    "size=96|itemCount=false|preview=thumbnail": __body18,
    // figma: Size=256, Item Count = No, Preview=Thumbnail
    "size=256|itemCount=false|preview=thumbnail": __body19,
    // figma: Size=36, Item Count = No, Preview=Filled
    "size=36|itemCount=false|preview=filled": __body20,
    // figma: Size=36, Item Count = No, Preview=None
    "size=36|itemCount=false|preview=none": __body21,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Folder;
