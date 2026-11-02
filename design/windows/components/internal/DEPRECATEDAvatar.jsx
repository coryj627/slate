import { __slateNorm } from './slateProps.jsx';
import { Person2 } from './Person2.jsx';

// figma node: 9000:1149 .[DEPRECATED] Avatar (91 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "size=" + __venc(p.size) + '|' + "color=" + __venc(p.color);

export function DEPRECATEDAvatar(_p = {}) {
  _p = __slateNorm(_p, {"layout":["image","icon","initials"],"size":["16","20","24","28","32","36","40","48","56","64","72","96","120"],"color":["neutral","brand","colored"]});
  const props = { ..._p, presence: _p.presence ?? false, layout: _p.layout ?? "image", size: _p.size ?? "16", color: _p.color ?? "neutral", activityRing: _p.activityRing ?? false, text: _p.text ?? "MB" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 36,
          top: 36,
          width: 48,
          height: 48,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"48"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 24,
          top: 24,
          width: 48,
          height: 48,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"48"} theme={"regular"} />}</div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 20,
          top: 20,
          width: 32,
          height: 32,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"32"} theme={"regular"} />}</div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 16,
          top: 16,
          width: 32,
          height: 32,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"32"} theme={"regular"} />}</div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 20,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 14,
          top: 14,
          width: 28,
          height: 28,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"28"} theme={"regular"} />}</div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 12,
          top: 12,
          width: 24,
          height: 24,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"24"} theme={"regular"} />}</div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 10,
          top: 10,
          width: 20,
          height: 20,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 8,
          top: 8,
          width: 20,
          height: 20,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 20,
        height: 20,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 15 11 C 16.104 11 17 11.896 17 13 C 17 14.691 16.167 15.967 14.865 16.797 C 13.583 17.614 11.855 18 10 18 C 8.145 18 6.417 17.614 5.135 16.797 C 3.833 15.967 3 14.691 3 13 C 3 11.887 3.903 11 5.009 11 L 15 11 Z M 5.009 12 C 4.448 12 4 12.447 4 13 C 4 14.309 4.622 15.283 5.673 15.953 C 6.743 16.636 8.265 17 10 17 C 11.735 17 13.257 16.636 14.327 15.953 C 15.378 15.283 16 14.309 16 13 C 16 12.448 15.552 12 15 12 L 5.009 12 Z M 10 2 C 12.209 2 14 3.791 14 6 C 14 8.209 12.209 10 10 10 C 7.791 10 6 8.209 6 6 C 6 3.791 7.791 2 10 2 Z M 10 3 C 8.343 3 7 4.343 7 6 C 7 7.657 8.343 9 10 9 C 11.657 9 13 7.657 13 6 C 13 4.343 11.657 3 10 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 20,
        height: 20,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 15 11 C 16.104 11 17 11.896 17 13 C 17 14.691 16.167 15.967 14.865 16.797 C 13.583 17.614 11.855 18 10 18 C 8.145 18 6.417 17.614 5.135 16.797 C 3.833 15.967 3 14.691 3 13 C 3 11.887 3.903 11 5.009 11 L 15 11 Z M 5.009 12 C 4.448 12 4 12.447 4 13 C 4 14.309 4.622 15.283 5.673 15.953 C 6.743 16.636 8.265 17 10 17 C 11.735 17 13.257 16.636 14.327 15.953 C 15.378 15.283 16 14.309 16 13 C 16 12.448 15.552 12 15 12 L 5.009 12 Z M 10 2 C 12.209 2 14 3.791 14 6 C 14 8.209 12.209 10 10 10 C 7.791 10 6 8.209 6 6 C 6 3.791 7.791 2 10 2 Z M 10 3 C 8.343 3 7 4.343 7 6 C 7 7.657 8.343 9 10 9 C 11.657 9 13 7.657 13 6 C 13 4.343 11.657 3 10 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 16,
          height: 16,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>{props.icon1 ?? <Person2 size={"16"} theme={"regular"} />}</div>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 11.5 8 C 12.328 8 13 8.672 13 9.5 L 13 10 C 13 11.971 11.14 14 8 14 C 4.86 14 3 11.971 3 10 L 3 9.5 C 3 8.672 3.672 8 4.5 8 L 11.5 8 Z M 4.5 9 C 4.224 9 4 9.224 4 9.5 L 4 10 C 4 11.438 5.432 13 8 13 C 10.568 13 12 11.438 12 10 L 12 9.5 C 12 9.224 11.776 9 11.5 9 L 4.5 9 Z M 8 1.5 C 9.519 1.5 10.75 2.731 10.75 4.25 C 10.75 5.769 9.519 7 8 7 C 6.481 7 5.25 5.769 5.25 4.25 C 5.25 2.731 6.481 1.5 8 1.5 Z M 8 2.5 C 7.034 2.5 6.25 3.284 6.25 4.25 C 6.25 5.216 7.034 6 8 6 C 8.966 6 9.75 5.216 9.75 4.25 C 9.75 3.284 8.966 2.5 8 2.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 10.5 8 C 11.328 8 12 8.672 12 9.5 C 12 10.616 11.541 11.511 10.788 12.115 C 10.047 12.71 9.053 13 8 13 C 6.947 13 5.953 12.71 5.212 12.115 C 4.459 11.511 4 10.616 4 9.5 C 4 8.672 4.672 8 5.5 8 L 10.5 8 Z M 5.5 9 C 5.224 9 5 9.224 5 9.5 C 5 10.317 5.324 10.923 5.838 11.335 C 6.363 11.757 7.12 12 8 12 C 8.88 12 9.637 11.757 10.162 11.335 C 10.676 10.923 11 10.317 11 9.5 C 11 9.224 10.776 9 10.5 9 L 5.5 9 Z M 8 3 C 9.105 3 10 3.895 10 5 C 10 6.105 9.105 7 8 7 C 6.895 7 6 6.105 6 5 C 6 3.895 6.895 3 8 3 Z M 8 4 C 7.448 4 7 4.448 7 5 C 7 5.552 7.448 6 8 6 C 8.552 6 9 5.552 9 5 C 9 4.448 8.552 4 8 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 44,
        width: 120,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        lineHeight: "32px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 32,
        width: 96,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        lineHeight: "32px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 72,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        lineHeight: "28px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 18,
        width: 64,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        lineHeight: "28px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 40,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 17,
        width: 56,
        height: 22,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        textAlign: "center",
        lineHeight: "22px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 14,
        width: 48,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 10,
        width: 40,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 8,
        width: 36,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={19.585} height={9.803} viewBox="0 0 19.585 9.803" fill="none" style={{
        position: "absolute",
        left: 6.505,
        top: 11.697,
        width: 19.585,
        height: 9.803,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 4.867 6.528 C 5.063 7.03 5.191 7.403 5.25 7.649 L 5.284 7.649 C 5.453 7.134 5.59 6.752 5.694 6.501 L 8.319 0 L 10.527 0 L 10.527 9.803 L 8.914 9.803 L 8.914 3.459 C 8.914 2.939 8.946 2.304 9.01 1.552 L 8.982 1.552 C 8.882 1.98 8.793 2.288 8.716 2.475 L 5.79 9.803 L 4.669 9.803 L 1.736 2.529 C 1.654 2.315 1.568 1.989 1.477 1.552 L 1.449 1.552 C 1.486 1.944 1.504 2.584 1.504 3.473 L 1.504 9.803 L 0 9.803 L 0 0 L 2.29 0 L 4.867 6.528 Z M 16.058 0 C 17.006 0 17.755 0.207 18.307 0.622 C 18.863 1.037 19.141 1.577 19.141 2.242 C 19.141 2.798 18.983 3.281 18.669 3.691 C 18.354 4.102 17.919 4.393 17.363 4.566 L 17.363 4.594 C 18.038 4.671 18.576 4.922 18.977 5.346 C 19.382 5.765 19.585 6.312 19.585 6.986 C 19.585 7.825 19.255 8.504 18.594 9.023 C 17.933 9.543 17.099 9.803 16.092 9.803 L 12.947 9.803 L 12.947 0 L 16.058 0 Z M 14.567 8.504 L 15.955 8.504 C 16.561 8.504 17.028 8.365 17.356 8.087 C 17.689 7.804 17.855 7.417 17.855 6.925 C 17.855 5.909 17.151 5.4 15.743 5.4 L 14.567 5.4 L 14.567 8.504 Z M 14.567 4.095 L 15.62 4.095 C 16.185 4.095 16.627 3.963 16.946 3.698 C 17.27 3.429 17.432 3.051 17.432 2.563 C 17.432 1.725 16.871 1.306 15.75 1.306 L 14.567 1.306 L 14.567 4.095 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={16.787} height={8.402} viewBox="0 0 16.787 8.402" fill="none" style={{
        position: "absolute",
        left: 5.861,
        top: 10.098,
        width: 16.787,
        height: 8.402,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 4.172 5.596 C 4.34 6.025 4.449 6.346 4.5 6.557 L 4.529 6.557 C 4.674 6.115 4.791 5.787 4.881 5.572 L 7.131 0 L 9.023 0 L 9.023 8.402 L 7.641 8.402 L 7.641 2.965 C 7.641 2.52 7.668 1.975 7.723 1.33 L 7.699 1.33 C 7.613 1.697 7.537 1.961 7.471 2.121 L 4.963 8.402 L 4.002 8.402 L 1.488 2.168 C 1.418 1.984 1.344 1.705 1.266 1.33 L 1.242 1.33 C 1.273 1.666 1.289 2.215 1.289 2.977 L 1.289 8.402 L 0 8.402 L 0 0 L 1.963 0 L 4.172 5.596 Z M 13.764 0 C 14.576 0 15.219 0.178 15.691 0.533 C 16.168 0.889 16.406 1.352 16.406 1.922 C 16.406 2.398 16.271 2.813 16.002 3.164 C 15.732 3.516 15.359 3.766 14.883 3.914 L 14.883 3.938 C 15.461 4.004 15.922 4.219 16.266 4.582 C 16.613 4.941 16.787 5.41 16.787 5.988 C 16.787 6.707 16.504 7.289 15.938 7.734 C 15.371 8.18 14.656 8.402 13.793 8.402 L 11.098 8.402 L 11.098 0 L 13.764 0 Z M 12.486 7.289 L 13.676 7.289 C 14.195 7.289 14.596 7.17 14.877 6.932 C 15.162 6.689 15.305 6.357 15.305 5.936 C 15.305 5.064 14.701 4.629 13.494 4.629 L 12.486 4.629 L 12.486 7.289 Z M 12.486 3.51 L 13.389 3.51 C 13.873 3.51 14.252 3.396 14.525 3.17 C 14.803 2.939 14.941 2.615 14.941 2.197 C 14.941 1.479 14.461 1.119 13.5 1.119 L 12.486 1.119 L 12.486 3.51 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 5,
        width: 24,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        lineHeight: "14px",
        color: "var(--neutral-foreground-3-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={13.989} height={7.002} viewBox="0 0 13.989 7.002" fill="none" style={{
        position: "absolute",
        left: 3.218,
        top: 6.498,
        width: 13.989,
        height: 7.002,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 3.477 4.663 C 3.617 5.021 3.708 5.288 3.75 5.464 L 3.774 5.464 C 3.895 5.096 3.993 4.823 4.067 4.644 L 5.942 0 L 7.52 0 L 7.52 7.002 L 6.367 7.002 L 6.367 2.471 C 6.367 2.1 6.39 1.646 6.436 1.108 L 6.416 1.108 C 6.344 1.414 6.281 1.634 6.226 1.768 L 4.136 7.002 L 3.335 7.002 L 1.24 1.807 C 1.182 1.654 1.12 1.421 1.055 1.108 L 1.035 1.108 C 1.061 1.388 1.074 1.846 1.074 2.48 L 1.074 7.002 L 0 7.002 L 0 0 L 1.636 0 L 3.477 4.663 Z M 11.47 0 C 12.147 0 12.682 0.148 13.076 0.444 C 13.473 0.741 13.672 1.126 13.672 1.602 C 13.672 1.999 13.56 2.344 13.335 2.637 C 13.11 2.93 12.799 3.138 12.402 3.262 L 12.402 3.281 C 12.884 3.337 13.268 3.516 13.555 3.818 C 13.844 4.118 13.989 4.508 13.989 4.99 C 13.989 5.589 13.753 6.074 13.281 6.445 C 12.809 6.816 12.214 7.002 11.494 7.002 L 9.248 7.002 L 9.248 0 L 11.47 0 Z M 10.405 6.074 L 11.396 6.074 C 11.829 6.074 12.163 5.975 12.397 5.776 C 12.635 5.575 12.754 5.298 12.754 4.946 C 12.754 4.22 12.251 3.857 11.245 3.857 L 10.405 3.857 L 10.405 6.074 Z M 10.405 2.925 L 11.157 2.925 C 11.561 2.925 11.877 2.83 12.104 2.642 C 12.336 2.45 12.451 2.179 12.451 1.831 C 12.451 1.232 12.051 0.933 11.25 0.933 L 10.405 0.933 L 10.405 2.925 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--neutral-background-6-rest-3)",
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={7.520} height={7.002} viewBox="0 0 7.520 7.002" fill="none" style={{
        position: "absolute",
        left: 5.744,
        top: 5.799,
        width: 7.52,
        height: 7.002,
        color: "var(--neutral-foreground-3-rest-3)",
      }}>
        <path d={"M 3.477 4.663 C 3.617 5.021 3.708 5.288 3.75 5.464 L 3.774 5.464 C 3.895 5.096 3.993 4.823 4.067 4.644 L 5.942 0 L 7.52 0 L 7.52 7.002 L 6.367 7.002 L 6.367 2.471 C 6.367 2.1 6.39 1.646 6.436 1.108 L 6.416 1.108 C 6.344 1.414 6.281 1.634 6.226 1.768 L 4.136 7.002 L 3.335 7.002 L 1.24 1.807 C 1.182 1.654 1.12 1.421 1.055 1.108 L 1.035 1.108 C 1.061 1.388 1.074 1.846 1.074 2.48 L 1.074 7.002 L 0 7.002 L 0 0 L 1.636 0 L 3.477 4.663 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 40,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 36,
          top: 36,
          width: 48,
          height: 48,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"48"} theme={"regular"} />}</div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 24,
          top: 24,
          width: 48,
          height: 48,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"48"} theme={"regular"} />}</div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 20,
          top: 20,
          width: 32,
          height: 32,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"32"} theme={"regular"} />}</div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 16,
          top: 16,
          width: 32,
          height: 32,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"32"} theme={"regular"} />}</div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 40,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 14,
          top: 14,
          width: 28,
          height: 28,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"28"} theme={"regular"} />}</div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 12,
          top: 12,
          width: 24,
          height: 24,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"24"} theme={"regular"} />}</div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 10,
          top: 10,
          width: 20,
          height: 20,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 8,
          top: 8,
          width: 20,
          height: 20,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon2 ?? <Person2 size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 20,
        height: 20,
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>
        <path d={"M 15 11 C 16.104 11 17 11.896 17 13 C 17 14.691 16.167 15.967 14.865 16.797 C 13.583 17.614 11.855 18 10 18 C 8.145 18 6.417 17.614 5.135 16.797 C 3.833 15.967 3 14.691 3 13 C 3 11.887 3.903 11 5.009 11 L 15 11 Z M 5.009 12 C 4.448 12 4 12.447 4 13 C 4 14.309 4.622 15.283 5.673 15.953 C 6.743 16.636 8.265 17 10 17 C 11.735 17 13.257 16.636 14.327 15.953 C 15.378 15.283 16 14.309 16 13 C 16 12.448 15.552 12 15 12 L 5.009 12 Z M 10 2 C 12.209 2 14 3.791 14 6 C 14 8.209 12.209 10 10 10 C 7.791 10 6 8.209 6 6 C 6 3.791 7.791 2 10 2 Z M 10 3 C 8.343 3 7 4.343 7 6 C 7 7.657 8.343 9 10 9 C 11.657 9 13 7.657 13 6 C 13 4.343 11.657 3 10 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 20,
        height: 20,
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>
        <path d={"M 15 11 C 16.104 11 17 11.896 17 13 C 17 14.691 16.167 15.967 14.865 16.797 C 13.583 17.614 11.855 18 10 18 C 8.145 18 6.417 17.614 5.135 16.797 C 3.833 15.967 3 14.691 3 13 C 3 11.887 3.903 11 5.009 11 L 15 11 Z M 5.009 12 C 4.448 12 4 12.447 4 13 C 4 14.309 4.622 15.283 5.673 15.953 C 6.743 16.636 8.265 17 10 17 C 11.735 17 13.257 16.636 14.327 15.953 C 15.378 15.283 16 14.309 16 13 C 16 12.448 15.552 12 15 12 L 5.009 12 Z M 10 2 C 12.209 2 14 3.791 14 6 C 14 8.209 12.209 10 10 10 C 7.791 10 6 8.209 6 6 C 6 3.791 7.791 2 10 2 Z M 10 3 C 8.343 3 7 4.343 7 6 C 7 7.657 8.343 9 10 9 C 11.657 9 13 7.657 13 6 C 13 4.343 11.657 3 10 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 16,
          height: 16,
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.icon1 ?? <Person2 size={"16"} theme={"regular"} />}</div>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>
        <path d={"M 11.5 8 C 12.328 8 13 8.672 13 9.5 L 13 10 C 13 11.971 11.14 14 8 14 C 4.86 14 3 11.971 3 10 L 3 9.5 C 3 8.672 3.672 8 4.5 8 L 11.5 8 Z M 4.5 9 C 4.224 9 4 9.224 4 9.5 L 4 10 C 4 11.438 5.432 13 8 13 C 10.568 13 12 11.438 12 10 L 12 9.5 C 12 9.224 11.776 9 11.5 9 L 4.5 9 Z M 8 1.5 C 9.519 1.5 10.75 2.731 10.75 4.25 C 10.75 5.769 9.519 7 8 7 C 6.481 7 5.25 5.769 5.25 4.25 C 5.25 2.731 6.481 1.5 8 1.5 Z M 8 2.5 C 7.034 2.5 6.25 3.284 6.25 4.25 C 6.25 5.216 7.034 6 8 6 C 8.966 6 9.75 5.216 9.75 4.25 C 9.75 3.284 8.966 2.5 8 2.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>
        <path d={"M 10.5 8 C 11.328 8 12 8.672 12 9.5 C 12 10.616 11.541 11.511 10.788 12.115 C 10.047 12.71 9.053 13 8 13 C 6.947 13 5.953 12.71 5.212 12.115 C 4.459 11.511 4 10.616 4 9.5 C 4 8.672 4.672 8 5.5 8 L 10.5 8 Z M 5.5 9 C 5.224 9 5 9.224 5 9.5 C 5 10.317 5.324 10.923 5.838 11.335 C 6.363 11.757 7.12 12 8 12 C 8.88 12 9.637 11.757 10.162 11.335 C 10.676 10.923 11 10.317 11 9.5 C 11 9.224 10.776 9 10.5 9 L 5.5 9 Z M 8 3 C 9.105 3 10 3.895 10 5 C 10 6.105 9.105 7 8 7 C 6.895 7 6 6.105 6 5 C 6 3.895 6.895 3 8 3 Z M 8 4 C 7.448 4 7 4.448 7 5 C 7 5.552 7.448 6 8 6 C 8.552 6 9 5.552 9 5 C 9 4.448 8.552 4 8 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 44,
        width: 120,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        lineHeight: "32px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 32,
        width: 96,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        lineHeight: "32px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 72,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        lineHeight: "28px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 18,
        width: 64,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        lineHeight: "28px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 40,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 17,
        width: 56,
        height: 22,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        textAlign: "center",
        lineHeight: "22px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 14,
        width: 48,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 10,
        width: 40,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 8,
        width: 36,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 5.5,
        width: 32,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 5.5,
        width: 28,
        height: 16,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 12,
        textAlign: "center",
        lineHeight: "16px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 5,
        width: 24,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        lineHeight: "14px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 2.5,
        width: 20,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        lineHeight: "14px",
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>{props.text}</span>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--brand-background-static-1-rest-2)",
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={7.520} height={7.002} viewBox="0 0 7.520 7.002" fill="none" style={{
        position: "absolute",
        left: 5.744,
        top: 5.799,
        width: 7.52,
        height: 7.002,
        color: "var(--neutral-foreground-on-brand-rest-3)",
      }}>
        <path d={"M 3.477 4.663 C 3.617 5.021 3.708 5.288 3.75 5.464 L 3.774 5.464 C 3.895 5.096 3.993 4.823 4.067 4.644 L 5.942 0 L 7.52 0 L 7.52 7.002 L 6.367 7.002 L 6.367 2.471 C 6.367 2.1 6.39 1.646 6.436 1.108 L 6.416 1.108 C 6.344 1.414 6.281 1.634 6.226 1.768 L 4.136 7.002 L 3.335 7.002 L 1.24 1.807 C 1.182 1.654 1.12 1.421 1.055 1.108 L 1.035 1.108 C 1.061 1.388 1.074 1.846 1.074 2.48 L 1.074 7.002 L 0 7.002 L 0 0 L 1.636 0 L 3.477 4.663 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--brand-stroke-compound-rest-3)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 36,
          top: 36,
          width: 48,
          height: 48,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"48"} theme={"regular"} />}</div>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 24,
          top: 24,
          width: 48,
          height: 48,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"48"} theme={"regular"} />}</div>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 20,
          top: 20,
          width: 32,
          height: 32,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"32"} theme={"regular"} />}</div>
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 16,
          top: 16,
          width: 32,
          height: 32,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"32"} theme={"regular"} />}</div>
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 40,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 14,
          top: 14,
          width: 28,
          height: 28,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"28"} theme={"regular"} />}</div>
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 12,
          top: 12,
          width: 24,
          height: 24,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"24"} theme={"regular"} />}</div>
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 10,
          top: 10,
          width: 20,
          height: 20,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <div style={{
          position: "absolute",
          left: 8,
          top: 8,
          width: 20,
          height: 20,
          color: "var(--avatar-foreground)",
        }}>{props.icon2 ?? <Person2 size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body73 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 6,
        top: 6,
        width: 20,
        height: 20,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 15 11 C 16.104 11 17 11.896 17 13 C 17 14.691 16.167 15.967 14.865 16.797 C 13.583 17.614 11.855 18 10 18 C 8.145 18 6.417 17.614 5.135 16.797 C 3.833 15.967 3 14.691 3 13 C 3 11.887 3.903 11 5.009 11 L 15 11 Z M 5.009 12 C 4.448 12 4 12.447 4 13 C 4 14.309 4.622 15.283 5.673 15.953 C 6.743 16.636 8.265 17 10 17 C 11.735 17 13.257 16.636 14.327 15.953 C 15.378 15.283 16 14.309 16 13 C 16 12.448 15.552 12 15 12 L 5.009 12 Z M 10 2 C 12.209 2 14 3.791 14 6 C 14 8.209 12.209 10 10 10 C 7.791 10 6 8.209 6 6 C 6 3.791 7.791 2 10 2 Z M 10 3 C 8.343 3 7 4.343 7 6 C 7 7.657 8.343 9 10 9 C 11.657 9 13 7.657 13 6 C 13 4.343 11.657 3 10 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body74 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 4,
        top: 4,
        width: 20,
        height: 20,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 15 11 C 16.104 11 17 11.896 17 13 C 17 14.691 16.167 15.967 14.865 16.797 C 13.583 17.614 11.855 18 10 18 C 8.145 18 6.417 17.614 5.135 16.797 C 3.833 15.967 3 14.691 3 13 C 3 11.887 3.903 11 5.009 11 L 15 11 Z M 5.009 12 C 4.448 12 4 12.447 4 13 C 4 14.309 4.622 15.283 5.673 15.953 C 6.743 16.636 8.265 17 10 17 C 11.735 17 13.257 16.636 14.327 15.953 C 15.378 15.283 16 14.309 16 13 C 16 12.448 15.552 12 15 12 L 5.009 12 Z M 10 2 C 12.209 2 14 3.791 14 6 C 14 8.209 12.209 10 10 10 C 7.791 10 6 8.209 6 6 C 6 3.791 7.791 2 10 2 Z M 10 3 C 8.343 3 7 4.343 7 6 C 7 7.657 8.343 9 10 9 C 11.657 9 13 7.657 13 6 C 13 4.343 11.657 3 10 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body75 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 16,
          height: 16,
          color: "var(--avatar-foreground)",
        }}>{props.icon1 ?? <Person2 size={"16"} theme={"regular"} />}</div>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body76 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 2,
        top: 2,
        width: 16,
        height: 16,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 11.5 8 C 12.328 8 13 8.672 13 9.5 L 13 10 C 13 11.971 11.14 14 8 14 C 4.86 14 3 11.971 3 10 L 3 9.5 C 3 8.672 3.672 8 4.5 8 L 11.5 8 Z M 4.5 9 C 4.224 9 4 9.224 4 9.5 L 4 10 C 4 11.438 5.432 13 8 13 C 10.568 13 12 11.438 12 10 L 12 9.5 C 12 9.224 11.776 9 11.5 9 L 4.5 9 Z M 8 1.5 C 9.519 1.5 10.75 2.731 10.75 4.25 C 10.75 5.769 9.519 7 8 7 C 6.481 7 5.25 5.769 5.25 4.25 C 5.25 2.731 6.481 1.5 8 1.5 Z M 8 2.5 C 7.034 2.5 6.25 3.284 6.25 4.25 C 6.25 5.216 7.034 6 8 6 C 8.966 6 9.75 5.216 9.75 4.25 C 9.75 3.284 8.966 2.5 8 2.5 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body77 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 10.5 8 C 11.328 8 12 8.672 12 9.5 C 12 10.616 11.541 11.511 10.788 12.115 C 10.047 12.71 9.053 13 8 13 C 6.947 13 5.953 12.71 5.212 12.115 C 4.459 11.511 4 10.616 4 9.5 C 4 8.672 4.672 8 5.5 8 L 10.5 8 Z M 5.5 9 C 5.224 9 5 9.224 5 9.5 C 5 10.317 5.324 10.923 5.838 11.335 C 6.363 11.757 7.12 12 8 12 C 8.88 12 9.637 11.757 10.162 11.335 C 10.676 10.923 11 10.317 11 9.5 C 11 9.224 10.776 9 10.5 9 L 5.5 9 Z M 8 3 C 9.105 3 10 3.895 10 5 C 10 6.105 9.105 7 8 7 C 6.895 7 6 6.105 6 5 C 6 3.895 6.895 3 8 3 Z M 8 4 C 7.448 4 7 4.448 7 5 C 7 5.552 7.448 6 8 6 C 8.552 6 9 5.552 9 5 C 9 4.448 8.552 4 8 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __body78 = () => (
    <div className={props.className} style={{
      width: 120,
      height: 120,
      position: "relative",
      ...props.style,
    }}>
      <svg width={120} height={120} viewBox="0 0 120 120" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 120,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 60 0 C 93.137 0 120 26.863 120 60 C 120 93.137 93.137 120 60 120 C 26.863 120 0 93.137 0 60 C 0 26.863 26.863 0 60 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 91,
        top: 92,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={136} height={136} viewBox="0 0 136 136" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 136,
        height: 136,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 68 0 C 105.555 0 136 30.445 136 68 C 136 105.555 105.555 136 68 136 C 30.445 136 0 105.555 0 68 C 0 30.445 30.445 0 68 0 Z M 68 4 C 32.654 4 4 32.654 4 68 C 4 103.346 32.654 132 68 132 C 103.346 132 132 103.346 132 68 C 132 32.654 103.346 4 68 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 44,
        width: 120,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        lineHeight: "32px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body79 = () => (
    <div className={props.className} style={{
      width: 96,
      height: 96,
      position: "relative",
      ...props.style,
    }}>
      <svg width={96} height={96} viewBox="0 0 96 96" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 96,
        height: 96,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 48 0 C 74.51 0 96 21.49 96 48 C 96 74.51 74.51 96 48 96 C 21.49 96 0 74.51 0 48 C 0 21.49 21.49 0 48 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 68,
        top: 68,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "relative",
          width: 28,
          height: 28,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={112} height={112} viewBox="0 0 112 112" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 112,
        height: 112,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 56 0 C 86.928 0 112 25.072 112 56 C 112 86.928 86.928 112 56 112 C 25.072 112 0 86.928 0 56 C 0 25.072 25.072 0 56 0 Z M 56 4 C 27.281 4 4 27.281 4 56 C 4 84.719 27.281 108 56 108 C 84.719 108 108 84.719 108 56 C 108 27.281 84.719 4 56 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 32,
        width: 96,
        height: 32,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        textAlign: "center",
        lineHeight: "32px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body80 = () => (
    <div className={props.className} style={{
      width: 72,
      height: 72,
      position: "relative",
      ...props.style,
    }}>
      <svg width={72} height={72} viewBox="0 0 72 72" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 72,
        height: 72,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 36 0 C 55.882 0 72 16.118 72 36 C 72 55.882 55.882 72 36 72 C 16.118 72 0 55.882 0 36 C 0 16.118 16.118 0 36 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 52,
        top: 52,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={88} height={88} viewBox="0 0 88 88" fill="none" style={{
        position: "absolute",
        left: -8,
        top: -8,
        width: 88,
        height: 88,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 44 0 C 68.301 0 88 19.699 88 44 C 88 68.301 68.301 88 44 88 C 19.699 88 0 68.301 0 44 C 0 19.699 19.699 0 44 0 Z M 44 4 C 21.909 4 4 21.909 4 44 C 4 66.091 21.909 84 44 84 C 66.091 84 84 66.091 84 44 C 84 21.909 66.091 4 44 4 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 22,
        width: 72,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        lineHeight: "28px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body81 = () => (
    <div className={props.className} style={{
      width: 64,
      height: 64,
      position: "relative",
      ...props.style,
    }}>
      <svg width={64} height={64} viewBox="0 0 64 64" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 64,
        height: 64,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 32 0 C 49.673 0 64 14.327 64 32 C 64 49.673 49.673 64 32 64 C 14.327 64 0 49.673 0 32 C 0 14.327 14.327 0 32 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 44,
        top: 44,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={76} height={76} viewBox="0 0 76 76" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 76,
        height: 76,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 38 0 C 58.987 0 76 17.013 76 38 C 76 58.987 58.987 76 38 76 C 17.013 76 0 58.987 0 38 C 0 17.013 17.013 0 38 0 Z M 38 3 C 18.67 3 3 18.67 3 38 C 3 57.33 18.67 73 38 73 C 57.33 73 73 57.33 73 38 C 73 18.67 57.33 3 38 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 18,
        width: 64,
        height: 28,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        textAlign: "center",
        lineHeight: "28px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body82 = () => (
    <div className={props.className} style={{
      width: 56,
      height: 56,
      position: "relative",
      ...props.style,
    }}>
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 56,
        height: 56,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 40,
        top: 40,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={68} height={68} viewBox="0 0 68 68" fill="none" style={{
        position: "absolute",
        left: -6,
        top: -6,
        width: 68,
        height: 68,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 34 0 C 52.778 0 68 15.222 68 34 C 68 52.778 52.778 68 34 68 C 15.222 68 0 52.778 0 34 C 0 15.222 15.222 0 34 0 Z M 34 3 C 16.879 3 3 16.879 3 34 C 3 51.121 16.879 65 34 65 C 51.121 65 65 51.121 65 34 C 65 16.879 51.121 3 34 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 17,
        width: 56,
        height: 22,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        textAlign: "center",
        lineHeight: "22px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body83 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      position: "relative",
      ...props.style,
    }}>
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 48,
        height: 48,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 32,
        top: 32,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          height: 16,
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 16,
            height: 16,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={56} height={56} viewBox="0 0 56 56" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 56,
        height: 56,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 28 0 C 43.464 0 56 12.536 56 28 C 56 43.464 43.464 56 28 56 C 12.536 56 0 43.464 0 28 C 0 12.536 12.536 0 28 0 Z M 28 2 C 13.641 2 2 13.641 2 28 C 2 42.359 13.641 54 28 54 C 42.359 54 54 42.359 54 28 C 54 13.641 42.359 2 28 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 14,
        width: 48,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body84 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      position: "relative",
      ...props.style,
    }}>
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 28,
        top: 28,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={48} height={48} viewBox="0 0 48 48" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 48,
        height: 48,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 24 0 C 37.255 0 48 10.745 48 24 C 48 37.255 37.255 48 24 48 C 10.745 48 0 37.255 0 24 C 0 10.745 10.745 0 24 0 Z M 24 2 C 11.85 2 2 11.85 2 24 C 2 36.15 11.85 46 24 46 C 36.15 46 46 36.15 46 24 C 46 11.85 36.15 2 24 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 10,
        width: 40,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body85 = () => (
    <div className={props.className} style={{
      width: 36,
      height: 36,
      position: "relative",
      ...props.style,
    }}>
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 36,
        height: 36,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 26,
        top: 26,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={44} height={44} viewBox="0 0 44 44" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 44,
        height: 44,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 22 0 C 34.15 0 44 9.85 44 22 C 44 34.15 34.15 44 22 44 C 9.85 44 0 34.15 0 22 C 0 9.85 9.85 0 22 0 Z M 22 2 C 10.954 2 2 10.954 2 22 C 2 33.046 10.954 42 22 42 C 33.046 42 42 33.046 42 22 C 42 10.954 33.046 2 22 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 8,
        width: 36,
        height: 20,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 14,
        textAlign: "center",
        lineHeight: "20px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body86 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 22,
        top: 22,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 40,
        height: 40,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 20 0 C 31.046 0 40 8.954 40 20 C 40 31.046 31.046 40 20 40 C 8.954 40 0 31.046 0 20 C 0 8.954 8.954 0 20 0 Z M 20 2 C 10.059 2 2 10.059 2 20 C 2 29.941 10.059 38 20 38 C 29.941 38 38 29.941 38 20 C 38 10.059 29.941 2 20 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={19.585} height={9.803} viewBox="0 0 19.585 9.803" fill="none" style={{
        position: "absolute",
        left: 6.505,
        top: 11.697,
        width: 19.585,
        height: 9.803,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 4.867 6.528 C 5.063 7.03 5.191 7.403 5.25 7.649 L 5.284 7.649 C 5.453 7.134 5.59 6.752 5.694 6.501 L 8.319 0 L 10.527 0 L 10.527 9.803 L 8.914 9.803 L 8.914 3.459 C 8.914 2.939 8.946 2.304 9.01 1.552 L 8.982 1.552 C 8.882 1.98 8.793 2.288 8.716 2.475 L 5.79 9.803 L 4.669 9.803 L 1.736 2.529 C 1.654 2.315 1.568 1.989 1.477 1.552 L 1.449 1.552 C 1.486 1.944 1.504 2.584 1.504 3.473 L 1.504 9.803 L 0 9.803 L 0 0 L 2.29 0 L 4.867 6.528 Z M 16.058 0 C 17.006 0 17.755 0.207 18.307 0.622 C 18.863 1.037 19.141 1.577 19.141 2.242 C 19.141 2.798 18.983 3.281 18.669 3.691 C 18.354 4.102 17.919 4.393 17.363 4.566 L 17.363 4.594 C 18.038 4.671 18.576 4.922 18.977 5.346 C 19.382 5.765 19.585 6.312 19.585 6.986 C 19.585 7.825 19.255 8.504 18.594 9.023 C 17.933 9.543 17.099 9.803 16.092 9.803 L 12.947 9.803 L 12.947 0 L 16.058 0 Z M 14.567 8.504 L 15.955 8.504 C 16.561 8.504 17.028 8.365 17.356 8.087 C 17.689 7.804 17.855 7.417 17.855 6.925 C 17.855 5.909 17.151 5.4 15.743 5.4 L 14.567 5.4 L 14.567 8.504 Z M 14.567 4.095 L 15.62 4.095 C 16.185 4.095 16.627 3.963 16.946 3.698 C 17.27 3.429 17.432 3.051 17.432 2.563 C 17.432 1.725 16.871 1.306 15.75 1.306 L 14.567 1.306 L 14.567 4.095 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body87 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <div style={{
          position: "relative",
          width: 10,
          height: 10,
          flexShrink: 0,
        }}>
          <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 10,
            height: 10,
            color: "var(--status-available-foreground-3-rest-3)",
          }}>
            <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      )}
      {props.activityRing && (
      <svg width={36} height={36} viewBox="0 0 36 36" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 36,
        height: 36,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 18 0 C 27.941 0 36 8.059 36 18 C 36 27.941 27.941 36 18 36 C 8.059 36 0 27.941 0 18 C 0 8.059 8.059 0 18 0 Z M 18 2 C 9.163 2 2 9.163 2 18 C 2 26.837 9.163 34 18 34 C 26.837 34 34 26.837 34 18 C 34 9.163 26.837 2 18 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={16.787} height={8.402} viewBox="0 0 16.787 8.402" fill="none" style={{
        position: "absolute",
        left: 5.861,
        top: 10.098,
        width: 16.787,
        height: 8.402,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 4.172 5.596 C 4.34 6.025 4.449 6.346 4.5 6.557 L 4.529 6.557 C 4.674 6.115 4.791 5.787 4.881 5.572 L 7.131 0 L 9.023 0 L 9.023 8.402 L 7.641 8.402 L 7.641 2.965 C 7.641 2.52 7.668 1.975 7.723 1.33 L 7.699 1.33 C 7.613 1.697 7.537 1.961 7.471 2.121 L 4.963 8.402 L 4.002 8.402 L 1.488 2.168 C 1.418 1.984 1.344 1.705 1.266 1.33 L 1.242 1.33 C 1.273 1.666 1.289 2.215 1.289 2.977 L 1.289 8.402 L 0 8.402 L 0 0 L 1.963 0 L 4.172 5.596 Z M 13.764 0 C 14.576 0 15.219 0.178 15.691 0.533 C 16.168 0.889 16.406 1.352 16.406 1.922 C 16.406 2.398 16.271 2.813 16.002 3.164 C 15.732 3.516 15.359 3.766 14.883 3.914 L 14.883 3.938 C 15.461 4.004 15.922 4.219 16.266 4.582 C 16.613 4.941 16.787 5.41 16.787 5.988 C 16.787 6.707 16.504 7.289 15.938 7.734 C 15.371 8.18 14.656 8.402 13.793 8.402 L 11.098 8.402 L 11.098 0 L 13.764 0 Z M 12.486 7.289 L 13.676 7.289 C 14.195 7.289 14.596 7.17 14.877 6.932 C 15.162 6.689 15.305 6.357 15.305 5.936 C 15.305 5.064 14.701 4.629 13.494 4.629 L 12.486 4.629 L 12.486 7.289 Z M 12.486 3.51 L 13.389 3.51 C 13.873 3.51 14.252 3.396 14.525 3.17 C 14.803 2.939 14.941 2.615 14.941 2.197 C 14.941 1.479 14.461 1.119 13.5 1.119 L 12.486 1.119 L 12.486 3.51 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body88 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 18,
        top: 18,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 32,
        height: 32,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 16 0 C 24.837 0 32 7.163 32 16 C 32 24.837 24.837 32 16 32 C 7.163 32 0 24.837 0 16 C 0 7.163 7.163 0 16 0 Z M 16 2 C 8.268 2 2 8.268 2 16 C 2 23.732 8.268 30 16 30 C 23.732 30 30 23.732 30 16 C 30 8.268 23.732 2 16 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <span style={{
        position: "absolute",
        left: 0,
        top: 5,
        width: 24,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 10,
        textAlign: "center",
        lineHeight: "14px",
        color: "var(--avatar-foreground)",
      }}>{props.text}</span>
    </div>
  );
  const __body89 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 14,
        top: 14,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 28,
        height: 28,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 Z M 14 2 C 7.373 2 2 7.373 2 14 C 2 20.627 7.373 26 14 26 C 20.627 26 26 20.627 26 14 C 26 7.373 20.627 2 14 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
      <svg width={13.989} height={7.002} viewBox="0 0 13.989 7.002" fill="none" style={{
        position: "absolute",
        left: 3.218,
        top: 6.498,
        width: 13.989,
        height: 7.002,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 3.477 4.663 C 3.617 5.021 3.708 5.288 3.75 5.464 L 3.774 5.464 C 3.895 5.096 3.993 4.823 4.067 4.644 L 5.942 0 L 7.52 0 L 7.52 7.002 L 6.367 7.002 L 6.367 2.471 C 6.367 2.1 6.39 1.646 6.436 1.108 L 6.416 1.108 C 6.344 1.414 6.281 1.634 6.226 1.768 L 4.136 7.002 L 3.335 7.002 L 1.24 1.807 C 1.182 1.654 1.12 1.421 1.055 1.108 L 1.035 1.108 C 1.061 1.388 1.074 1.846 1.074 2.48 L 1.074 7.002 L 0 7.002 L 0 0 L 1.636 0 L 3.477 4.663 Z M 11.47 0 C 12.147 0 12.682 0.148 13.076 0.444 C 13.473 0.741 13.672 1.126 13.672 1.602 C 13.672 1.999 13.56 2.344 13.335 2.637 C 13.11 2.93 12.799 3.138 12.402 3.262 L 12.402 3.281 C 12.884 3.337 13.268 3.516 13.555 3.818 C 13.844 4.118 13.989 4.508 13.989 4.99 C 13.989 5.589 13.753 6.074 13.281 6.445 C 12.809 6.816 12.214 7.002 11.494 7.002 L 9.248 7.002 L 9.248 0 L 11.47 0 Z M 10.405 6.074 L 11.396 6.074 C 11.829 6.074 12.163 5.975 12.397 5.776 C 12.635 5.575 12.754 5.298 12.754 4.946 C 12.754 4.22 12.251 3.857 11.245 3.857 L 10.405 3.857 L 10.405 6.074 Z M 10.405 2.925 L 11.157 2.925 C 11.561 2.925 11.877 2.83 12.104 2.642 C 12.336 2.45 12.451 2.179 12.451 1.831 C 12.451 1.232 12.051 0.933 11.25 0.933 L 10.405 0.933 L 10.405 2.925 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body90 = () => (
    <div className={props.className} style={{
      width: 16,
      height: 16,
      position: "relative",
      ...props.style,
    }}>
      <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 16,
        color: "var(--avatar-background)",
      }}>
        <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <svg width={7.520} height={7.002} viewBox="0 0 7.520 7.002" fill="none" style={{
        position: "absolute",
        left: 5.744,
        top: 5.799,
        width: 7.52,
        height: 7.002,
        color: "var(--avatar-foreground)",
      }}>
        <path d={"M 3.477 4.663 C 3.617 5.021 3.708 5.288 3.75 5.464 L 3.774 5.464 C 3.895 5.096 3.993 4.823 4.067 4.644 L 5.942 0 L 7.52 0 L 7.52 7.002 L 6.367 7.002 L 6.367 2.471 C 6.367 2.1 6.39 1.646 6.436 1.108 L 6.416 1.108 C 6.344 1.414 6.281 1.634 6.226 1.768 L 4.136 7.002 L 3.335 7.002 L 1.24 1.807 C 1.182 1.654 1.12 1.421 1.055 1.108 L 1.035 1.108 C 1.061 1.388 1.074 1.846 1.074 2.48 L 1.074 7.002 L 0 7.002 L 0 0 L 1.636 0 L 3.477 4.663 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.presence && (
      <div style={{
        position: "absolute",
        left: 10,
        top: 10,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest-3)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.activityRing && (
      <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
        position: "absolute",
        left: -4,
        top: -4,
        width: 24,
        height: 24,
        color: "var(--avatar-active-stroke)",
      }}>
        <path d={"M 12 0 C 18.627 0 24 5.373 24 12 C 24 18.627 18.627 24 12 24 C 5.373 24 0 18.627 0 12 C 0 5.373 5.373 0 12 0 Z M 12 2 C 6.477 2 2 6.477 2 12 C 2 17.523 6.477 22 12 22 C 17.523 22 22 17.523 22 12 C 22 6.477 17.523 2 12 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      )}
    </div>
  );
  const __impls = {
    // figma: Layout=Icon, Size=120, Color=Neutral
    "layout=icon|size=120|color=neutral": __body0,
    // figma: Layout=Icon, Size=96, Color=Neutral
    "layout=icon|size=96|color=neutral": __body1,
    // figma: Layout=Icon, Size=72, Color=Neutral
    "layout=icon|size=72|color=neutral": __body2,
    // figma: Layout=Icon, Size=64, Color=Neutral
    "layout=icon|size=64|color=neutral": __body3,
    // figma: Layout=Icon, Size=56, Color=Neutral
    "layout=icon|size=56|color=neutral": __body4,
    // figma: Layout=Icon, Size=48, Color=Neutral
    "layout=icon|size=48|color=neutral": __body5,
    // figma: Layout=Icon, Size=40, Color=Neutral
    "layout=icon|size=40|color=neutral": __body6,
    // figma: Layout=Icon, Size=36, Color=Neutral
    "layout=icon|size=36|color=neutral": __body7,
    // figma: Layout=Icon, Size=32, Color=Neutral
    "layout=icon|size=32|color=neutral": __body8,
    // figma: Layout=Icon, Size=28, Color=Neutral
    "layout=icon|size=28|color=neutral": __body9,
    // figma: Layout=Icon, Size=24, Color=Neutral
    "layout=icon|size=24|color=neutral": __body10,
    // figma: Layout=Icon, Size=20, Color=Neutral
    "layout=icon|size=20|color=neutral": __body11,
    // figma: Layout=Icon, Size=16, Color=Neutral
    "layout=icon|size=16|color=neutral": __body12,
    // figma: Layout=Initials, Size=120, Color=Neutral
    "layout=initials|size=120|color=neutral": __body13,
    // figma: Layout=Initials, Size=96, Color=Neutral
    "layout=initials|size=96|color=neutral": __body14,
    // figma: Layout=Initials, Size=72, Color=Neutral
    "layout=initials|size=72|color=neutral": __body15,
    // figma: Layout=Initials, Size=64, Color=Neutral
    "layout=initials|size=64|color=neutral": __body16,
    // figma: Layout=Initials, Size=56, Color=Neutral
    "layout=initials|size=56|color=neutral": __body17,
    // figma: Layout=Initials, Size=48, Color=Neutral
    "layout=initials|size=48|color=neutral": __body18,
    // figma: Layout=Initials, Size=40, Color=Neutral
    "layout=initials|size=40|color=neutral": __body19,
    // figma: Layout=Initials, Size=36, Color=Neutral
    "layout=initials|size=36|color=neutral": __body20,
    // figma: Layout=Initials, Size=32, Color=Neutral
    "layout=initials|size=32|color=neutral": __body21,
    // figma: Layout=Initials, Size=28, Color=Neutral
    "layout=initials|size=28|color=neutral": __body22,
    // figma: Layout=Initials, Size=24, Color=Neutral
    "layout=initials|size=24|color=neutral": __body23,
    // figma: Layout=Initials, Size=20, Color=Neutral
    "layout=initials|size=20|color=neutral": __body24,
    // figma: Layout=Image, Size=120, Color=Neutral
    "layout=image|size=120|color=neutral": __body25,
    // figma: Layout=Image, Size=96, Color=Neutral
    "layout=image|size=96|color=neutral": __body26,
    // figma: Layout=Initials, Size=16, Color=Neutral
    "layout=initials|size=16|color=neutral": __body27,
    // figma: Layout=Image, Size=72, Color=Neutral
    "layout=image|size=72|color=neutral": __body28,
    // figma: Layout=Image, Size=64, Color=Neutral
    "layout=image|size=64|color=neutral": __body29,
    // figma: Layout=Image, Size=56, Color=Neutral
    "layout=image|size=56|color=neutral": __body30,
    // figma: Layout=Image, Size=48, Color=Neutral
    "layout=image|size=48|color=neutral": __body31,
    // figma: Layout=Image, Size=40, Color=Neutral
    "layout=image|size=40|color=neutral": __body32,
    // figma: Layout=Image, Size=36, Color=Neutral
    "layout=image|size=36|color=neutral": __body33,
    // figma: Layout=Image, Size=32, Color=Neutral
    "layout=image|size=32|color=neutral": __body34,
    // figma: Layout=Image, Size=28, Color=Neutral
    "layout=image|size=28|color=neutral": __body35,
    // figma: Layout=Image, Size=24, Color=Neutral
    "layout=image|size=24|color=neutral": __body36,
    // figma: Layout=Image, Size=20, Color=Neutral
    "layout=image|size=20|color=neutral": __body37,
    // figma: Layout=Image, Size=16, Color=Neutral
    "layout=image|size=16|color=neutral": __body38,
    // figma: Layout=Icon, Size=120, Color=Brand
    "layout=icon|size=120|color=brand": __body39,
    // figma: Layout=Icon, Size=96, Color=Brand
    "layout=icon|size=96|color=brand": __body40,
    // figma: Layout=Icon, Size=72, Color=Brand
    "layout=icon|size=72|color=brand": __body41,
    // figma: Layout=Icon, Size=64, Color=Brand
    "layout=icon|size=64|color=brand": __body42,
    // figma: Layout=Icon, Size=56, Color=Brand
    "layout=icon|size=56|color=brand": __body43,
    // figma: Layout=Icon, Size=48, Color=Brand
    "layout=icon|size=48|color=brand": __body44,
    // figma: Layout=Icon, Size=40, Color=Brand
    "layout=icon|size=40|color=brand": __body45,
    // figma: Layout=Icon, Size=36, Color=Brand
    "layout=icon|size=36|color=brand": __body46,
    // figma: Layout=Icon, Size=32, Color=Brand
    "layout=icon|size=32|color=brand": __body47,
    // figma: Layout=Icon, Size=28, Color=Brand
    "layout=icon|size=28|color=brand": __body48,
    // figma: Layout=Icon, Size=24, Color=Brand
    "layout=icon|size=24|color=brand": __body49,
    // figma: Layout=Icon, Size=20, Color=Brand
    "layout=icon|size=20|color=brand": __body50,
    // figma: Layout=Icon, Size=16, Color=Brand
    "layout=icon|size=16|color=brand": __body51,
    // figma: Layout=Initials, Size=120, Color=Brand
    "layout=initials|size=120|color=brand": __body52,
    // figma: Layout=Initials, Size=96, Color=Brand
    "layout=initials|size=96|color=brand": __body53,
    // figma: Layout=Initials, Size=72, Color=Brand
    "layout=initials|size=72|color=brand": __body54,
    // figma: Layout=Initials, Size=64, Color=Brand
    "layout=initials|size=64|color=brand": __body55,
    // figma: Layout=Initials, Size=56, Color=Brand
    "layout=initials|size=56|color=brand": __body56,
    // figma: Layout=Initials, Size=48, Color=Brand
    "layout=initials|size=48|color=brand": __body57,
    // figma: Layout=Initials, Size=40, Color=Brand
    "layout=initials|size=40|color=brand": __body58,
    // figma: Layout=Initials, Size=36, Color=Brand
    "layout=initials|size=36|color=brand": __body59,
    // figma: Layout=Initials, Size=32, Color=Brand
    "layout=initials|size=32|color=brand": __body60,
    // figma: Layout=Initials, Size=28, Color=Brand
    "layout=initials|size=28|color=brand": __body61,
    // figma: Layout=Initials, Size=24, Color=Brand
    "layout=initials|size=24|color=brand": __body62,
    // figma: Layout=Initials, Size=20, Color=Brand
    "layout=initials|size=20|color=brand": __body63,
    // figma: Layout=Initials, Size=16, Color=Brand
    "layout=initials|size=16|color=brand": __body64,
    // figma: Layout=Icon, Size=120, Color=Colored
    "layout=icon|size=120|color=colored": __body65,
    // figma: Layout=Icon, Size=96, Color=Colored
    "layout=icon|size=96|color=colored": __body66,
    // figma: Layout=Icon, Size=72, Color=Colored
    "layout=icon|size=72|color=colored": __body67,
    // figma: Layout=Icon, Size=64, Color=Colored
    "layout=icon|size=64|color=colored": __body68,
    // figma: Layout=Icon, Size=56, Color=Colored
    "layout=icon|size=56|color=colored": __body69,
    // figma: Layout=Icon, Size=48, Color=Colored
    "layout=icon|size=48|color=colored": __body70,
    // figma: Layout=Icon, Size=40, Color=Colored
    "layout=icon|size=40|color=colored": __body71,
    // figma: Layout=Icon, Size=36, Color=Colored
    "layout=icon|size=36|color=colored": __body72,
    // figma: Layout=Icon, Size=32, Color=Colored
    "layout=icon|size=32|color=colored": __body73,
    // figma: Layout=Icon, Size=28, Color=Colored
    "layout=icon|size=28|color=colored": __body74,
    // figma: Layout=Icon, Size=24, Color=Colored
    "layout=icon|size=24|color=colored": __body75,
    // figma: Layout=Icon, Size=20, Color=Colored
    "layout=icon|size=20|color=colored": __body76,
    // figma: Layout=Icon, Size=16, Color=Colored
    "layout=icon|size=16|color=colored": __body77,
    // figma: Layout=Initials, Size=120, Color=Colored
    "layout=initials|size=120|color=colored": __body78,
    // figma: Layout=Initials, Size=96, Color=Colored
    "layout=initials|size=96|color=colored": __body79,
    // figma: Layout=Initials, Size=72, Color=Colored
    "layout=initials|size=72|color=colored": __body80,
    // figma: Layout=Initials, Size=64, Color=Colored
    "layout=initials|size=64|color=colored": __body81,
    // figma: Layout=Initials, Size=56, Color=Colored
    "layout=initials|size=56|color=colored": __body82,
    // figma: Layout=Initials, Size=48, Color=Colored
    "layout=initials|size=48|color=colored": __body83,
    // figma: Layout=Initials, Size=40, Color=Colored
    "layout=initials|size=40|color=colored": __body84,
    // figma: Layout=Initials, Size=36, Color=Colored
    "layout=initials|size=36|color=colored": __body85,
    // figma: Layout=Initials, Size=32, Color=Colored
    "layout=initials|size=32|color=colored": __body86,
    // figma: Layout=Initials, Size=28, Color=Colored
    "layout=initials|size=28|color=colored": __body87,
    // figma: Layout=Initials, Size=24, Color=Colored
    "layout=initials|size=24|color=colored": __body88,
    // figma: Layout=Initials, Size=20, Color=Colored
    "layout=initials|size=20|color=colored": __body89,
    // figma: Layout=Initials, Size=16, Color=Colored
    "layout=initials|size=16|color=colored": __body90,
  };
  return (__impls[__vkey(props)] ?? __body38)();
}
export default DEPRECATEDAvatar;
