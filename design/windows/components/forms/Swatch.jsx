import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron } from '../internal/Chevron.jsx';
import { Chevron2 } from '../internal/Chevron2.jsx';
import { Prohibited } from '../internal/Prohibited.jsx';

// figma node: 9116:20586 Swatch (48 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size) + '|' + "style2=" + __venc(p.style2);

export function Swatch(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","hover","pressed","disabled","empty","transparent"],"size":["extrasmall","sm","medium (default)","lg"],"style2":["color","icon","image"]});
  const props = { ..._p, state: _p.state ?? "rest", size: _p.size ?? "extrasmall", style2: _p.style2 ?? "color", focus: _p.focus ?? false, selected: _p.selected ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 20,
          height: 20,
          color: "var(--colors-neutral-white)",
        }}>{props.icon1 ?? <Prohibited size={"20"} theme={"filled"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        outline: "1px dashed var(--neutral-foreground-4-rest)",
        outlineOffset: "-1px",
      }} />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        outline: "1px dashed var(--neutral-foreground-4-rest)",
        outlineOffset: "-1px",
      }} />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        outline: "1px dashed var(--neutral-foreground-4-rest)",
        outlineOffset: "-1px",
      }} />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        outline: "1px dashed var(--neutral-foreground-4-rest)",
        outlineOffset: "-1px",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 24,
          height: 24,
          color: "var(--colors-neutral-white)",
        }}>{props.icon1 ?? <Prohibited size={"24"} theme={"filled"} />}</div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 16,
          height: 16,
          color: "var(--colors-neutral-white)",
        }}>{props.icon1 ?? <Prohibited size={"16"} theme={"filled"} />}</div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 12,
          height: 12,
          color: "var(--colors-neutral-white)",
        }}>{props.icon1 ?? <Prohibited size={"12"} theme={"filled"} />}</div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 32 0 L 39 0 L 39 -7 L 32 -7 L 32 0 Z M 32 32 L 32 39 L 39 39 L 39 32 L 32 32 Z M 0 32 L -7 32 L -7 39 L 0 39 L 0 32 Z M 0 0 L 0 7 L 32 7 L 32 0 L 32 -7 L 0 -7 L 0 0 Z M 32 0 L 25 0 L 25 32 L 32 32 L 39 32 L 39 0 L 32 0 Z M 32 32 L 32 25 L 0 25 L 0 32 L 0 39 L 32 39 L 32 32 Z M 0 32 L 7 32 L 7 0 L 0 0 L -7 0 L -7 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 32 0 L 39 0 L 39 -7 L 32 -7 L 32 0 Z M 32 32 L 32 39 L 39 39 L 39 32 L 32 32 Z M 0 32 L -7 32 L -7 39 L 0 39 L 0 32 Z M 0 0 L 0 7 L 32 7 L 32 0 L 32 -7 L 0 -7 L 0 0 Z M 32 0 L 25 0 L 25 32 L 32 32 L 39 32 L 39 0 L 32 0 Z M 32 32 L 32 25 L 0 25 L 0 32 L 0 39 L 32 39 L 32 32 Z M 0 32 L 7 32 L 7 0 L 0 0 L -7 0 L -7 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        borderTop: "3px solid var(--brand-stroke-compound-pressed)",
        borderRight: "3px solid var(--brand-stroke-compound-pressed)",
        borderBottom: "3px solid var(--brand-stroke-compound-pressed)",
        borderLeft: "3px solid var(--brand-stroke-compound-pressed)",
        boxShadow: "inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 24,
          height: 24,
          color: "var(--colors-neutral-white)",
        }}>{props.icon1 ?? <Chevron direction={"down"} size={"24"} theme={"filled"} />}</div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 32 0 L 39 0 L 39 -7 L 32 -7 L 32 0 Z M 32 32 L 32 39 L 39 39 L 39 32 L 32 32 Z M 0 32 L -7 32 L -7 39 L 0 39 L 0 32 Z M 0 0 L 0 7 L 32 7 L 32 0 L 32 -7 L 0 -7 L 0 0 Z M 32 0 L 25 0 L 25 32 L 32 32 L 39 32 L 39 0 L 32 0 Z M 32 32 L 32 25 L 0 25 L 0 32 L 0 39 L 32 39 L 32 32 Z M 0 32 L 7 32 L 7 0 L 0 0 L -7 0 L -7 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 24 0 L 31 0 L 31 -7 L 24 -7 L 24 0 Z M 24 24 L 24 31 L 31 31 L 31 24 L 24 24 Z M 0 24 L -7 24 L -7 31 L 0 31 L 0 24 Z M 0 0 L 0 7 L 24 7 L 24 0 L 24 -7 L 0 -7 L 0 0 Z M 24 0 L 17 0 L 17 24 L 24 24 L 31 24 L 31 0 L 24 0 Z M 24 24 L 24 17 L 0 17 L 0 24 L 0 31 L 24 31 L 24 24 Z M 0 24 L 7 24 L 7 0 L 0 0 L -7 0 L -7 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 24 0 L 31 0 L 31 -7 L 24 -7 L 24 0 Z M 24 24 L 24 31 L 31 31 L 31 24 L 24 24 Z M 0 24 L -7 24 L -7 31 L 0 31 L 0 24 Z M 0 0 L 0 7 L 24 7 L 24 0 L 24 -7 L 0 -7 L 0 0 Z M 24 0 L 17 0 L 17 24 L 24 24 L 31 24 L 31 0 L 24 0 Z M 24 24 L 24 17 L 0 17 L 0 24 L 0 31 L 24 31 L 24 24 Z M 0 24 L 7 24 L 7 0 L 0 0 L -7 0 L -7 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
          borderTop: "3px solid var(--brand-stroke-compound-pressed)",
          borderRight: "3px solid var(--brand-stroke-compound-pressed)",
          borderBottom: "3px solid var(--brand-stroke-compound-pressed)",
          borderLeft: "3px solid var(--brand-stroke-compound-pressed)",
          boxShadow: "inset 0px 0px 0px 4px rgb(255,255,255)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 16,
            height: 16,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"16"} theme={"filled"} />}</div>
      </div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 24 0 L 31 0 L 31 -7 L 24 -7 L 24 0 Z M 24 24 L 24 31 L 31 31 L 31 24 L 24 24 Z M 0 24 L -7 24 L -7 31 L 0 31 L 0 24 Z M 0 0 L 0 7 L 24 7 L 24 0 L 24 -7 L 0 -7 L 0 0 Z M 24 0 L 17 0 L 17 24 L 24 24 L 31 24 L 31 0 L 24 0 Z M 24 24 L 24 17 L 0 17 L 0 24 L 0 31 L 24 31 L 24 24 Z M 0 24 L 7 24 L 7 0 L 0 0 L -7 0 L -7 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 20 0 L 25 0 L 25 -5 L 20 -5 L 20 0 Z M 20 20 L 20 25 L 25 25 L 25 20 L 20 20 Z M 0 20 L -5 20 L -5 25 L 0 25 L 0 20 Z M 0 0 L 0 5 L 20 5 L 20 0 L 20 -5 L 0 -5 L 0 0 Z M 20 0 L 15 0 L 15 20 L 20 20 L 25 20 L 25 0 L 20 0 Z M 20 20 L 20 15 L 0 15 L 0 20 L 0 25 L 20 25 L 20 20 Z M 0 20 L 5 20 L 5 0 L 0 0 L -5 0 L -5 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 20 0 L 25 0 L 25 -5 L 20 -5 L 20 0 Z M 20 20 L 20 25 L 25 25 L 25 20 L 20 20 Z M 0 20 L -5 20 L -5 25 L 0 25 L 0 20 Z M 0 0 L 0 5 L 20 5 L 20 0 L 20 -5 L 0 -5 L 0 0 Z M 20 0 L 15 0 L 15 20 L 20 20 L 25 20 L 25 0 L 20 0 Z M 20 20 L 20 15 L 0 15 L 0 20 L 0 25 L 20 25 L 20 20 Z M 0 20 L 5 20 L 5 0 L 0 0 L -5 0 L -5 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
          boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 3px rgb(255,255,255)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
      </div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 20 0 L 25 0 L 25 -5 L 20 -5 L 20 0 Z M 20 20 L 20 25 L 25 25 L 25 20 L 20 20 Z M 0 20 L -5 20 L -5 25 L 0 25 L 0 20 Z M 0 0 L 0 5 L 20 5 L 20 0 L 20 -5 L 0 -5 L 0 0 Z M 20 0 L 15 0 L 15 20 L 20 20 L 25 20 L 25 0 L 20 0 Z M 20 20 L 20 15 L 0 15 L 0 20 L 0 25 L 20 25 L 20 20 Z M 0 20 L 5 20 L 5 0 L 0 0 L -5 0 L -5 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 28 0 L 35 0 L 35 -7 L 28 -7 L 28 0 Z M 28 28 L 28 35 L 35 35 L 35 28 L 28 28 Z M 0 28 L -7 28 L -7 35 L 0 35 L 0 28 Z M 0 0 L 0 7 L 28 7 L 28 0 L 28 -7 L 0 -7 L 0 0 Z M 28 0 L 21 0 L 21 28 L 28 28 L 35 28 L 35 0 L 28 0 Z M 28 28 L 28 21 L 0 21 L 0 28 L 0 35 L 28 35 L 28 28 Z M 0 28 L 7 28 L 7 0 L 0 0 L -7 0 L -7 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 28 0 L 35 0 L 35 -7 L 28 -7 L 28 0 Z M 28 28 L 28 35 L 35 35 L 35 28 L 28 28 Z M 0 28 L -7 28 L -7 35 L 0 35 L 0 28 Z M 0 0 L 0 7 L 28 7 L 28 0 L 28 -7 L 0 -7 L 0 0 Z M 28 0 L 21 0 L 21 28 L 28 28 L 35 28 L 35 0 L 28 0 Z M 28 28 L 28 21 L 0 21 L 0 28 L 0 35 L 28 35 L 28 28 Z M 0 28 L 7 28 L 7 0 L 0 0 L -7 0 L -7 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
          boxShadow: "inset 0 0 0 3px var(--brand-stroke-compound-pressed), inset 0px 0px 0px 4px rgb(255,255,255)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 20,
            height: 20,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"filled"} />}</div>
      </div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-pressed)",
        borderRight: "4px solid var(--brand-background-compound-pressed)",
        borderBottom: "4px solid var(--brand-background-compound-pressed)",
        borderLeft: "4px solid var(--brand-background-compound-pressed)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -7 L -7 -7 L -7 0 L 0 0 Z M 28 0 L 35 0 L 35 -7 L 28 -7 L 28 0 Z M 28 28 L 28 35 L 35 35 L 35 28 L 28 28 Z M 0 28 L -7 28 L -7 35 L 0 35 L 0 28 Z M 0 0 L 0 7 L 28 7 L 28 0 L 28 -7 L 0 -7 L 0 0 Z M 28 0 L 21 0 L 21 28 L 28 28 L 35 28 L 35 0 L 28 0 Z M 28 28 L 28 21 L 0 21 L 0 28 L 0 35 L 28 35 L 28 28 Z M 0 28 L 7 28 L 7 0 L 0 0 L -7 0 L -7 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-rest), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 32 0 L 38 0 L 38 -6 L 32 -6 L 32 0 Z M 32 32 L 32 38 L 38 38 L 38 32 L 32 32 Z M 0 32 L -6 32 L -6 38 L 0 38 L 0 32 Z M 0 0 L 0 6 L 32 6 L 32 0 L 32 -6 L 0 -6 L 0 0 Z M 32 0 L 26 0 L 26 32 L 32 32 L 38 32 L 38 0 L 32 0 Z M 32 32 L 32 26 L 0 26 L 0 32 L 0 38 L 32 38 L 32 32 Z M 0 32 L 6 32 L 6 0 L 0 0 L -6 0 L -6 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-rest), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 32 0 L 38 0 L 38 -6 L 32 -6 L 32 0 Z M 32 32 L 32 38 L 38 38 L 38 32 L 32 32 Z M 0 32 L -6 32 L -6 38 L 0 38 L 0 32 Z M 0 0 L 0 6 L 32 6 L 32 0 L 32 -6 L 0 -6 L 0 0 Z M 32 0 L 26 0 L 26 32 L 32 32 L 38 32 L 38 0 L 32 0 Z M 32 32 L 32 26 L 0 26 L 0 32 L 0 38 L 32 38 L 32 32 Z M 0 32 L 6 32 L 6 0 L 0 0 L -6 0 L -6 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
          borderTop: "2px solid var(--brand-stroke-compound-rest)",
          borderRight: "2px solid var(--brand-stroke-compound-rest)",
          borderBottom: "2px solid var(--brand-stroke-compound-rest)",
          borderLeft: "2px solid var(--brand-stroke-compound-rest)",
          boxShadow: "inset 0px 0px 0px 3px rgb(255,255,255)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 24,
            height: 24,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"24"} theme={"filled"} />}</div>
      </div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 32 0 L 38 0 L 38 -6 L 32 -6 L 32 0 Z M 32 32 L 32 38 L 38 38 L 38 32 L 32 32 Z M 0 32 L -6 32 L -6 38 L 0 38 L 0 32 Z M 0 0 L 0 6 L 32 6 L 32 0 L 32 -6 L 0 -6 L 0 0 Z M 32 0 L 26 0 L 26 32 L 32 32 L 38 32 L 38 0 L 32 0 Z M 32 32 L 32 26 L 0 26 L 0 32 L 0 38 L 32 38 L 32 32 Z M 0 32 L 6 32 L 6 0 L 0 0 L -6 0 L -6 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-rest), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 24 0 L 30 0 L 30 -6 L 24 -6 L 24 0 Z M 24 24 L 24 30 L 30 30 L 30 24 L 24 24 Z M 0 24 L -6 24 L -6 30 L 0 30 L 0 24 Z M 0 0 L 0 6 L 24 6 L 24 0 L 24 -6 L 0 -6 L 0 0 Z M 24 0 L 18 0 L 18 24 L 24 24 L 30 24 L 30 0 L 24 0 Z M 24 24 L 24 18 L 0 18 L 0 24 L 0 30 L 24 30 L 24 24 Z M 0 24 L 6 24 L 6 0 L 0 0 L -6 0 L -6 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-rest), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 24 0 L 30 0 L 30 -6 L 24 -6 L 24 0 Z M 24 24 L 24 30 L 30 30 L 30 24 L 24 24 Z M 0 24 L -6 24 L -6 30 L 0 30 L 0 24 Z M 0 0 L 0 6 L 24 6 L 24 0 L 24 -6 L 0 -6 L 0 0 Z M 24 0 L 18 0 L 18 24 L 24 24 L 30 24 L 30 0 L 24 0 Z M 24 24 L 24 18 L 0 18 L 0 24 L 0 30 L 24 30 L 24 24 Z M 0 24 L 6 24 L 6 0 L 0 0 L -6 0 L -6 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
          boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-rest), inset 0px 0px 0px 3px rgb(255,255,255)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 16,
            height: 16,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"16"} theme={"filled"} />}</div>
      </div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 24 0 L 30 0 L 30 -6 L 24 -6 L 24 0 Z M 24 24 L 24 30 L 30 30 L 30 24 L 24 24 Z M 0 24 L -6 24 L -6 30 L 0 30 L 0 24 Z M 0 0 L 0 6 L 24 6 L 24 0 L 24 -6 L 0 -6 L 0 0 Z M 24 0 L 18 0 L 18 24 L 24 24 L 30 24 L 30 0 L 24 0 Z M 24 24 L 24 18 L 0 18 L 0 24 L 0 30 L 24 30 L 24 24 Z M 0 24 L 6 24 L 6 0 L 0 0 L -6 0 L -6 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 1px var(--brand-stroke-compound-rest), inset 0px 0px 0px 2px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-hover)",
        borderRight: "3px solid var(--brand-background-compound-hover)",
        borderBottom: "3px solid var(--brand-background-compound-hover)",
        borderLeft: "3px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -4 L -4 -4 L -4 0 L 0 0 Z M 20 0 L 24 0 L 24 -4 L 20 -4 L 20 0 Z M 20 20 L 20 24 L 24 24 L 24 20 L 20 20 Z M 0 20 L -4 20 L -4 24 L 0 24 L 0 20 Z M 0 0 L 0 4 L 20 4 L 20 0 L 20 -4 L 0 -4 L 0 0 Z M 20 0 L 16 0 L 16 20 L 20 20 L 24 20 L 24 0 L 20 0 Z M 20 20 L 20 16 L 0 16 L 0 20 L 0 24 L 20 24 L 20 20 Z M 0 20 L 4 20 L 4 0 L 0 0 L -4 0 L -4 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        boxShadow: "inset 0 0 0 1px var(--brand-stroke-compound-rest), inset 0px 0px 0px 2px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-hover)",
        borderRight: "3px solid var(--brand-background-compound-hover)",
        borderBottom: "3px solid var(--brand-background-compound-hover)",
        borderLeft: "3px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -4 L -4 -4 L -4 0 L 0 0 Z M 20 0 L 24 0 L 24 -4 L 20 -4 L 20 0 Z M 20 20 L 20 24 L 24 24 L 24 20 L 20 20 Z M 0 20 L -4 20 L -4 24 L 0 24 L 0 20 Z M 0 0 L 0 4 L 20 4 L 20 0 L 20 -4 L 0 -4 L 0 0 Z M 20 0 L 16 0 L 16 20 L 20 20 L 24 20 L 24 0 L 20 0 Z M 20 20 L 20 16 L 0 16 L 0 20 L 0 24 L 20 24 L 20 20 Z M 0 20 L 4 20 L 4 0 L 0 0 L -4 0 L -4 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
          boxShadow: "inset 0 0 0 1px var(--brand-stroke-compound-rest), inset 0px 0px 0px 2px rgb(255,255,255)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
      </div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-hover)",
        borderRight: "3px solid var(--brand-background-compound-hover)",
        borderBottom: "3px solid var(--brand-background-compound-hover)",
        borderLeft: "3px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -4 L -4 -4 L -4 0 L 0 0 Z M 20 0 L 24 0 L 24 -4 L 20 -4 L 20 0 Z M 20 20 L 20 24 L 24 24 L 24 20 L 20 20 Z M 0 20 L -4 20 L -4 24 L 0 24 L 0 20 Z M 0 0 L 0 4 L 20 4 L 20 0 L 20 -4 L 0 -4 L 0 0 Z M 20 0 L 16 0 L 16 20 L 20 20 L 24 20 L 24 0 L 20 0 Z M 20 20 L 20 16 L 0 16 L 0 20 L 0 24 L 20 24 L 20 20 Z M 0 20 L 4 20 L 4 0 L 0 0 L -4 0 L -4 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        boxShadow: "inset 0 0 0 2px var(--brand-stroke-compound-rest), inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 28 0 L 34 0 L 34 -6 L 28 -6 L 28 0 Z M 28 28 L 28 34 L 34 34 L 34 28 L 28 28 Z M 0 28 L -6 28 L -6 34 L 0 34 L 0 28 Z M 0 0 L 0 6 L 28 6 L 28 0 L 28 -6 L 0 -6 L 0 0 Z M 28 0 L 22 0 L 22 28 L 28 28 L 34 28 L 34 0 L 28 0 Z M 28 28 L 28 22 L 0 22 L 0 28 L 0 34 L 28 34 L 28 28 Z M 0 28 L 6 28 L 6 0 L 0 0 L -6 0 L -6 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        borderTop: "2px solid var(--brand-stroke-compound-rest)",
        borderRight: "2px solid var(--brand-stroke-compound-rest)",
        borderBottom: "2px solid var(--brand-stroke-compound-rest)",
        borderLeft: "2px solid var(--brand-stroke-compound-rest)",
        boxShadow: "inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 28 0 L 34 0 L 34 -6 L 28 -6 L 28 0 Z M 28 28 L 28 34 L 34 34 L 34 28 L 28 28 Z M 0 28 L -6 28 L -6 34 L 0 34 L 0 28 Z M 0 0 L 0 6 L 28 6 L 28 0 L 28 -6 L 0 -6 L 0 0 Z M 28 0 L 22 0 L 22 28 L 28 28 L 34 28 L 34 0 L 28 0 Z M 28 28 L 28 22 L 0 22 L 0 28 L 0 34 L 28 34 L 28 28 Z M 0 28 L 6 28 L 6 0 L 0 0 L -6 0 L -6 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        borderTop: "2px solid var(--brand-stroke-compound-rest)",
        borderRight: "2px solid var(--brand-stroke-compound-rest)",
        borderBottom: "2px solid var(--brand-stroke-compound-rest)",
        borderLeft: "2px solid var(--brand-stroke-compound-rest)",
        boxShadow: "inset 0px 0px 0px 3px rgb(255,255,255)",
      }} />
      <div style={{
          position: "absolute",
          left: 4,
          top: 4,
          width: 20,
          height: 20,
          color: "var(--colors-neutral-white)",
        }}>{props.icon1 ?? <Chevron direction={"down"} size={"20"} theme={"filled"} />}</div>
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "4px solid var(--brand-background-compound-hover)",
        borderRight: "4px solid var(--brand-background-compound-hover)",
        borderBottom: "4px solid var(--brand-background-compound-hover)",
        borderLeft: "4px solid var(--brand-background-compound-hover)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -6 L -6 -6 L -6 0 L 0 0 Z M 28 0 L 34 0 L 34 -6 L 28 -6 L 28 0 Z M 28 28 L 28 34 L 34 34 L 34 28 L 28 28 Z M 0 28 L -6 28 L -6 34 L 0 34 L 0 28 Z M 0 0 L 0 6 L 28 6 L 28 0 L 28 -6 L 0 -6 L 0 0 Z M 28 0 L 22 0 L 22 28 L 28 28 L 34 28 L 34 0 L 28 0 Z M 28 28 L 28 22 L 0 22 L 0 28 L 0 34 L 28 34 L 28 28 Z M 0 28 L 6 28 L 6 0 L 0 0 L -6 0 L -6 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 20 0 L 23 0 L 23 -3 L 20 -3 L 20 0 Z M 20 20 L 20 23 L 23 23 L 23 20 L 20 20 Z M 0 20 L -3 20 L -3 23 L 0 23 L 0 20 Z M 0 0 L 0 3 L 20 3 L 20 0 L 20 -3 L 0 -3 L 0 0 Z M 20 0 L 17 0 L 17 20 L 20 20 L 23 20 L 23 0 L 20 0 Z M 20 20 L 20 17 L 0 17 L 0 20 L 0 23 L 20 23 L 20 20 Z M 0 20 L 3 20 L 3 0 L 0 0 L -3 0 L -3 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 20 0 L 25 0 L 25 -5 L 20 -5 L 20 0 Z M 20 20 L 20 25 L 25 25 L 25 20 L 20 20 Z M 0 20 L -5 20 L -5 25 L 0 25 L 0 20 Z M 0 0 L 0 5 L 20 5 L 20 0 L 20 -5 L 0 -5 L 0 0 Z M 20 0 L 15 0 L 15 20 L 20 20 L 25 20 L 25 0 L 20 0 Z M 20 20 L 20 15 L 0 15 L 0 20 L 0 25 L 20 25 L 20 20 Z M 0 20 L 5 20 L 5 0 L 0 0 L -5 0 L -5 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 20 0 L 23 0 L 23 -3 L 20 -3 L 20 0 Z M 20 20 L 20 23 L 23 23 L 23 20 L 20 20 Z M 0 20 L -3 20 L -3 23 L 0 23 L 0 20 Z M 0 0 L 0 3 L 20 3 L 20 0 L 20 -3 L 0 -3 L 0 0 Z M 20 0 L 17 0 L 17 20 L 20 20 L 23 20 L 23 0 L 20 0 Z M 20 20 L 20 17 L 0 17 L 0 20 L 0 23 L 20 23 L 20 20 Z M 0 20 L 3 20 L 3 0 L 0 0 L -3 0 L -3 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 20 0 L 25 0 L 25 -5 L 20 -5 L 20 0 Z M 20 20 L 20 25 L 25 25 L 25 20 L 20 20 Z M 0 20 L -5 20 L -5 25 L 0 25 L 0 20 Z M 0 0 L 0 5 L 20 5 L 20 0 L 20 -5 L 0 -5 L 0 0 Z M 20 0 L 15 0 L 15 20 L 20 20 L 25 20 L 25 0 L 20 0 Z M 20 20 L 20 15 L 0 15 L 0 20 L 0 25 L 20 25 L 20 20 Z M 0 20 L 5 20 L 5 0 L 0 0 L -5 0 L -5 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 12,
            height: 12,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"filled"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 20 0 L 23 0 L 23 -3 L 20 -3 L 20 0 Z M 20 20 L 20 23 L 23 23 L 23 20 L 20 20 Z M 0 20 L -3 20 L -3 23 L 0 23 L 0 20 Z M 0 0 L 0 3 L 20 3 L 20 0 L 20 -3 L 0 -3 L 0 0 Z M 20 0 L 17 0 L 17 20 L 20 20 L 23 20 L 23 0 L 20 0 Z M 20 20 L 20 17 L 0 17 L 0 20 L 0 23 L 20 23 L 20 20 Z M 0 20 L 3 20 L 3 0 L 0 0 L -3 0 L -3 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 20,
        height: 20,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 20 0 L 25 0 L 25 -5 L 20 -5 L 20 0 Z M 20 20 L 20 25 L 25 25 L 25 20 L 20 20 Z M 0 20 L -5 20 L -5 25 L 0 25 L 0 20 Z M 0 0 L 0 5 L 20 5 L 20 0 L 20 -5 L 0 -5 L 0 0 Z M 20 0 L 15 0 L 15 20 L 20 20 L 25 20 L 25 0 L 20 0 Z M 20 20 L 20 15 L 0 15 L 0 20 L 0 25 L 20 25 L 20 20 Z M 0 20 L 5 20 L 5 0 L 0 0 L -5 0 L -5 20 L 0 20 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 24 0 L 27 0 L 27 -3 L 24 -3 L 24 0 Z M 24 24 L 24 27 L 27 27 L 27 24 L 24 24 Z M 0 24 L -3 24 L -3 27 L 0 27 L 0 24 Z M 0 0 L 0 3 L 24 3 L 24 0 L 24 -3 L 0 -3 L 0 0 Z M 24 0 L 21 0 L 21 24 L 24 24 L 27 24 L 27 0 L 24 0 Z M 24 24 L 24 21 L 0 21 L 0 24 L 0 27 L 24 27 L 24 24 Z M 0 24 L 3 24 L 3 0 L 0 0 L -3 0 L -3 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 24 0 L 29 0 L 29 -5 L 24 -5 L 24 0 Z M 24 24 L 24 29 L 29 29 L 29 24 L 24 24 Z M 0 24 L -5 24 L -5 29 L 0 29 L 0 24 Z M 0 0 L 0 5 L 24 5 L 24 0 L 24 -5 L 0 -5 L 0 0 Z M 24 0 L 19 0 L 19 24 L 24 24 L 29 24 L 29 0 L 24 0 Z M 24 24 L 24 19 L 0 19 L 0 24 L 0 29 L 24 29 L 24 24 Z M 0 24 L 5 24 L 5 0 L 0 0 L -5 0 L -5 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
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
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 24 0 L 27 0 L 27 -3 L 24 -3 L 24 0 Z M 24 24 L 24 27 L 27 27 L 27 24 L 24 24 Z M 0 24 L -3 24 L -3 27 L 0 27 L 0 24 Z M 0 0 L 0 3 L 24 3 L 24 0 L 24 -3 L 0 -3 L 0 0 Z M 24 0 L 21 0 L 21 24 L 24 24 L 27 24 L 27 0 L 24 0 Z M 24 24 L 24 21 L 0 21 L 0 24 L 0 27 L 24 27 L 24 24 Z M 0 24 L 3 24 L 3 0 L 0 0 L -3 0 L -3 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 24 0 L 29 0 L 29 -5 L 24 -5 L 24 0 Z M 24 24 L 24 29 L 29 29 L 29 24 L 24 24 Z M 0 24 L -5 24 L -5 29 L 0 29 L 0 24 Z M 0 0 L 0 5 L 24 5 L 24 0 L 24 -5 L 0 -5 L 0 0 Z M 24 0 L 19 0 L 19 24 L 24 24 L 29 24 L 29 0 L 24 0 Z M 24 24 L 24 19 L 0 19 L 0 24 L 0 29 L 24 29 L 24 24 Z M 0 24 L 5 24 L 5 0 L 0 0 L -5 0 L -5 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"16"} theme={"filled"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 24 0 L 27 0 L 27 -3 L 24 -3 L 24 0 Z M 24 24 L 24 27 L 27 27 L 27 24 L 24 24 Z M 0 24 L -3 24 L -3 27 L 0 27 L 0 24 Z M 0 0 L 0 3 L 24 3 L 24 0 L 24 -3 L 0 -3 L 0 0 Z M 24 0 L 21 0 L 21 24 L 24 24 L 27 24 L 27 0 L 24 0 Z M 24 24 L 24 21 L 0 21 L 0 24 L 0 27 L 24 27 L 24 24 Z M 0 24 L 3 24 L 3 0 L 0 0 L -3 0 L -3 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 24 0 L 29 0 L 29 -5 L 24 -5 L 24 0 Z M 24 24 L 24 29 L 29 29 L 29 24 L 24 24 Z M 0 24 L -5 24 L -5 29 L 0 29 L 0 24 Z M 0 0 L 0 5 L 24 5 L 24 0 L 24 -5 L 0 -5 L 0 0 Z M 24 0 L 19 0 L 19 24 L 24 24 L 29 24 L 29 0 L 24 0 Z M 24 24 L 24 19 L 0 19 L 0 24 L 0 29 L 24 29 L 24 24 Z M 0 24 L 5 24 L 5 0 L 0 0 L -5 0 L -5 24 L 0 24 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 28 0 L 31 0 L 31 -3 L 28 -3 L 28 0 Z M 28 28 L 28 31 L 31 31 L 31 28 L 28 28 Z M 0 28 L -3 28 L -3 31 L 0 31 L 0 28 Z M 0 0 L 0 3 L 28 3 L 28 0 L 28 -3 L 0 -3 L 0 0 Z M 28 0 L 25 0 L 25 28 L 28 28 L 31 28 L 31 0 L 28 0 Z M 28 28 L 28 25 L 0 25 L 0 28 L 0 31 L 28 31 L 28 28 Z M 0 28 L 3 28 L 3 0 L 0 0 L -3 0 L -3 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 28 0 L 33 0 L 33 -5 L 28 -5 L 28 0 Z M 28 28 L 28 33 L 33 33 L 33 28 L 28 28 Z M 0 28 L -5 28 L -5 33 L 0 33 L 0 28 Z M 0 0 L 0 5 L 28 5 L 28 0 L 28 -5 L 0 -5 L 0 0 Z M 28 0 L 23 0 L 23 28 L 28 28 L 33 28 L 33 0 L 28 0 Z M 28 28 L 28 23 L 0 23 L 0 28 L 0 33 L 28 33 L 28 28 Z M 0 28 L 5 28 L 5 0 L 0 0 L -5 0 L -5 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 28 0 L 31 0 L 31 -3 L 28 -3 L 28 0 Z M 28 28 L 28 31 L 31 31 L 31 28 L 28 28 Z M 0 28 L -3 28 L -3 31 L 0 31 L 0 28 Z M 0 0 L 0 3 L 28 3 L 28 0 L 28 -3 L 0 -3 L 0 0 Z M 28 0 L 25 0 L 25 28 L 28 28 L 31 28 L 31 0 L 28 0 Z M 28 28 L 28 25 L 0 25 L 0 28 L 0 31 L 28 31 L 28 28 Z M 0 28 L 3 28 L 3 0 L 0 0 L -3 0 L -3 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 28 0 L 33 0 L 33 -5 L 28 -5 L 28 0 Z M 28 28 L 28 33 L 33 33 L 33 28 L 28 28 Z M 0 28 L -5 28 L -5 33 L 0 33 L 0 28 Z M 0 0 L 0 5 L 28 5 L 28 0 L 28 -5 L 0 -5 L 0 0 Z M 28 0 L 23 0 L 23 28 L 28 28 L 33 28 L 33 0 L 28 0 Z M 28 28 L 28 23 L 0 23 L 0 28 L 0 33 L 28 33 L 28 28 Z M 0 28 L 5 28 L 5 0 L 0 0 L -5 0 L -5 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
        display: "flex",
        flexDirection: "row",
        padding: "4px 4px 4px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"filled"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 28 0 L 31 0 L 31 -3 L 28 -3 L 28 0 Z M 28 28 L 28 31 L 31 31 L 31 28 L 28 28 Z M 0 28 L -3 28 L -3 31 L 0 31 L 0 28 Z M 0 0 L 0 3 L 28 3 L 28 0 L 28 -3 L 0 -3 L 0 0 Z M 28 0 L 25 0 L 25 28 L 28 28 L 31 28 L 31 0 L 28 0 Z M 28 28 L 28 25 L 0 25 L 0 28 L 0 31 L 28 31 L 28 28 Z M 0 28 L 3 28 L 3 0 L 0 0 L -3 0 L -3 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 28,
        height: 28,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={28} height={28} viewBox="0 0 28 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 28 0 L 33 0 L 33 -5 L 28 -5 L 28 0 Z M 28 28 L 28 33 L 33 33 L 33 28 L 28 28 Z M 0 28 L -5 28 L -5 33 L 0 33 L 0 28 Z M 0 0 L 0 5 L 28 5 L 28 0 L 28 -5 L 0 -5 L 0 0 Z M 28 0 L 23 0 L 23 28 L 28 28 L 33 28 L 33 0 L 28 0 Z M 28 28 L 28 23 L 0 23 L 0 28 L 0 33 L 28 33 L 28 28 Z M 0 28 L 5 28 L 5 0 L 0 0 L -5 0 L -5 28 L 0 28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        backgroundColor: "var(--colors-shared-hot-pink-primary)",
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 32 0 L 35 0 L 35 -3 L 32 -3 L 32 0 Z M 32 32 L 32 35 L 35 35 L 35 32 L 32 32 Z M 0 32 L -3 32 L -3 35 L 0 35 L 0 32 Z M 0 0 L 0 3 L 32 3 L 32 0 L 32 -3 L 0 -3 L 0 0 Z M 32 0 L 29 0 L 29 32 L 32 32 L 35 32 L 35 0 L 32 0 Z M 32 32 L 32 29 L 0 29 L 0 32 L 0 35 L 32 35 L 32 32 Z M 0 32 L 3 32 L 3 0 L 0 0 L -3 0 L -3 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 32 0 L 37 0 L 37 -5 L 32 -5 L 32 0 Z M 32 32 L 32 37 L 37 37 L 37 32 L 32 32 Z M 0 32 L -5 32 L -5 37 L 0 37 L 0 32 Z M 0 0 L 0 5 L 32 5 L 32 0 L 32 -5 L 0 -5 L 0 0 Z M 32 0 L 27 0 L 27 32 L 32 32 L 37 32 L 37 0 L 32 0 Z M 32 32 L 32 27 L 0 27 L 0 32 L 0 37 L 32 37 L 32 32 Z M 0 32 L 5 32 L 5 0 L 0 0 L -5 0 L -5 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div className="fig-asset-5179c625c37061a3" style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
      }} />
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 32 0 L 35 0 L 35 -3 L 32 -3 L 32 0 Z M 32 32 L 32 35 L 35 35 L 35 32 L 32 32 Z M 0 32 L -3 32 L -3 35 L 0 35 L 0 32 Z M 0 0 L 0 3 L 32 3 L 32 0 L 32 -3 L 0 -3 L 0 0 Z M 32 0 L 29 0 L 29 32 L 32 32 L 35 32 L 35 0 L 32 0 Z M 32 32 L 32 29 L 0 29 L 0 32 L 0 35 L 32 35 L 32 32 Z M 0 32 L 3 32 L 3 0 L 0 0 L -3 0 L -3 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }} />
      )}
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "var(--colors-shared-hot-pink-primary)",
        }} />
        <div style={{
            position: "absolute",
            left: 4,
            top: 4,
            width: 24,
            height: 24,
            color: "var(--colors-neutral-white)",
          }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"24"} theme={"filled"} />}</div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderTop: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "3px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "3px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -3 L -3 -3 L -3 0 L 0 0 Z M 32 0 L 35 0 L 35 -3 L 32 -3 L 32 0 Z M 32 32 L 32 35 L 35 35 L 35 32 L 32 32 Z M 0 32 L -3 32 L -3 35 L 0 35 L 0 32 Z M 0 0 L 0 3 L 32 3 L 32 0 L 32 -3 L 0 -3 L 0 0 Z M 32 0 L 29 0 L 29 32 L 32 32 L 35 32 L 35 0 L 32 0 Z M 32 32 L 32 29 L 0 29 L 0 32 L 0 35 L 32 35 L 32 32 Z M 0 32 L 3 32 L 3 0 L 0 0 L -3 0 L -3 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.selected && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0.056,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderTop: "3px solid var(--brand-background-compound-rest)",
        borderRight: "3px solid var(--brand-background-compound-rest)",
        borderBottom: "3px solid var(--brand-background-compound-rest)",
        borderLeft: "3px solid var(--brand-background-compound-rest)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 0 0 L 0 -5 L -5 -5 L -5 0 L 0 0 Z M 32 0 L 37 0 L 37 -5 L 32 -5 L 32 0 Z M 32 32 L 32 37 L 37 37 L 37 32 L 32 32 Z M 0 32 L -5 32 L -5 37 L 0 37 L 0 32 Z M 0 0 L 0 5 L 32 5 L 32 0 L 32 -5 L 0 -5 L 0 0 Z M 32 0 L 27 0 L 27 32 L 32 32 L 37 32 L 37 0 L 32 0 Z M 32 32 L 32 27 L 0 27 L 0 32 L 0 37 L 32 37 L 32 32 Z M 0 32 L 5 32 L 5 0 L 0 0 L -5 0 L -5 32 L 0 32 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: 28,
      height: 28,
      overflow: "hidden",
      position: "relative",
      color: "rgb(219,0,0)",
      ...props.style,
    }}>
      <svg width={38.070} height={2} viewBox="0 -1 38.070 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-0.710,0.704,-0.704,-0.710,27.042,0)",
        transformOrigin: "0 0",
        width: 38.07,
        height: 2,
      }}>
        <path d={"M 0 -1 L 0 0 L 38.07 0 L 38.07 -1 L 38.07 -2 L 0 -2 L 0 -1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 28,
        height: 28,
        borderTop: "1px solid var(--colors-neutral-grey-50)",
        borderRight: "1px solid var(--colors-neutral-grey-50)",
        borderBottom: "1px solid var(--colors-neutral-grey-50)",
        borderLeft: "1px solid var(--colors-neutral-grey-50)",
      }} />
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      overflow: "hidden",
      position: "relative",
      color: "rgb(219,0,0)",
      ...props.style,
    }}>
      <svg width={32.631} height={2} viewBox="0 -1 32.631 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-0.710,0.704,-0.704,-0.710,23.179,0)",
        transformOrigin: "0 0",
        width: 32.631,
        height: 2,
      }}>
        <path d={"M 0 -1 L 0 0 L 32.631 0 L 32.631 -1 L 32.631 -2 L 0 -2 L 0 -1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        borderTop: "1px solid var(--colors-neutral-grey-50)",
        borderRight: "1px solid var(--colors-neutral-grey-50)",
        borderBottom: "1px solid var(--colors-neutral-grey-50)",
        borderLeft: "1px solid var(--colors-neutral-grey-50)",
      }} />
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: 20,
      height: 20,
      overflow: "hidden",
      position: "relative",
      color: "rgb(219,0,0)",
      ...props.style,
    }}>
      <svg width={27.193} height={2} viewBox="0 -1 27.193 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-0.710,0.704,-0.704,-0.710,19.316,0)",
        transformOrigin: "0 0",
        width: 27.193,
        height: 2,
      }}>
        <path d={"M 0 -1 L 0 0 L 27.193 0 L 27.193 -1 L 27.193 -2 L 0 -2 L 0 -1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 20,
        height: 20,
        borderTop: "1px solid var(--colors-neutral-grey-50)",
        borderRight: "1px solid var(--colors-neutral-grey-50)",
        borderBottom: "1px solid var(--colors-neutral-grey-50)",
        borderLeft: "1px solid var(--colors-neutral-grey-50)",
      }} />
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: 32,
      height: 32,
      overflow: "hidden",
      position: "relative",
      color: "rgb(219,0,0)",
      ...props.style,
    }}>
      <svg width={43.509} height={2} viewBox="0 -1 43.509 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(-0.710,0.704,-0.704,-0.710,30.905,0)",
        transformOrigin: "0 0",
        width: 43.509,
        height: 2,
      }}>
        <path d={"M 0 -1 L 0 0 L 43.509 0 L 43.509 -1 L 43.509 -2 L 0 -2 L 0 -1 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        borderTop: "1px solid var(--colors-neutral-grey-50)",
        borderRight: "1px solid var(--colors-neutral-grey-50)",
        borderBottom: "1px solid var(--colors-neutral-grey-50)",
        borderLeft: "1px solid var(--colors-neutral-grey-50)",
      }} />
    </div>
  );
  const __impls = {
    // figma: State=Disabled, Size=Medium (Default), Style=Color
    "state=disabled|size=medium (default)|style2=color": __body0,
    // figma: State=Empty, Size=Medium (Default), Style=Color
    "state=empty|size=medium (default)|style2=color": __body1,
    // figma: State=Empty, Size=Large, Style=Color
    "state=empty|size=lg|style2=color": __body2,
    // figma: State=Empty, Size=Small, Style=Color
    "state=empty|size=sm|style2=color": __body3,
    // figma: State=Empty, Size=ExtraSmall, Style=Color
    "state=empty|size=extrasmall|style2=color": __body4,
    // figma: State=Disabled, Size=Large, Style=Color
    "state=disabled|size=lg|style2=color": __body5,
    // figma: State=Disabled, Size=Small, Style=Color
    "state=disabled|size=sm|style2=color": __body6,
    // figma: State=Disabled, Size=ExtraSmall, Style=Color
    "state=disabled|size=extrasmall|style2=color": __body7,
    // figma: State=Pressed, Size=Large, Style=Color
    "state=pressed|size=lg|style2=color": __body8,
    // figma: State=Pressed, Size=Large, Style=Image
    "state=pressed|size=lg|style2=image": __body9,
    // figma: State=Pressed, Size=Large, Style=Icon
    "state=pressed|size=lg|style2=icon": __body10,
    // figma: State=Pressed, Size=Small, Style=Color
    "state=pressed|size=sm|style2=color": __body11,
    // figma: State=Pressed, Size=Small, Style=Image
    "state=pressed|size=sm|style2=image": __body12,
    // figma: State=Pressed, Size=Small, Style=Icon
    "state=pressed|size=sm|style2=icon": __body13,
    // figma: State=Pressed, Size=ExtraSmall, Style=Color
    "state=pressed|size=extrasmall|style2=color": __body14,
    // figma: State=Pressed, Size=ExtraSmall, Style=Image
    "state=pressed|size=extrasmall|style2=image": __body15,
    // figma: State=Pressed, Size=ExtraSmall, Style=Icon
    "state=pressed|size=extrasmall|style2=icon": __body16,
    // figma: State=Pressed, Size=Medium (Default), Style=Color
    "state=pressed|size=medium (default)|style2=color": __body17,
    // figma: State=Pressed, Size=Medium (Default), Style=Image
    "state=pressed|size=medium (default)|style2=image": __body18,
    // figma: State=Pressed, Size=Medium (Default), Style=Icon
    "state=pressed|size=medium (default)|style2=icon": __body19,
    // figma: State=Hover, Size=Large, Style=Color
    "state=hover|size=lg|style2=color": __body20,
    // figma: State=Hover, Size=Large, Style=Image
    "state=hover|size=lg|style2=image": __body21,
    // figma: State=Hover, Size=Large, Style=Icon
    "state=hover|size=lg|style2=icon": __body22,
    // figma: State=Hover, Size=Small, Style=Color
    "state=hover|size=sm|style2=color": __body23,
    // figma: State=Hover, Size=Small, Style=Image
    "state=hover|size=sm|style2=image": __body24,
    // figma: State=Hover, Size=Small, Style=Icon
    "state=hover|size=sm|style2=icon": __body25,
    // figma: State=Hover, Size=ExtraSmall, Style=Color
    "state=hover|size=extrasmall|style2=color": __body26,
    // figma: State=Hover, Size=ExtraSmall, Style=Image
    "state=hover|size=extrasmall|style2=image": __body27,
    // figma: State=Hover, Size=ExtraSmall, Style=Icon
    "state=hover|size=extrasmall|style2=icon": __body28,
    // figma: State=Hover, Size=Medium (Default), Style=Color
    "state=hover|size=medium (default)|style2=color": __body29,
    // figma: State=Hover, Size=Medium (Default), Style=Image
    "state=hover|size=medium (default)|style2=image": __body30,
    // figma: State=Hover, Size=Medium (Default), Style=Icon
    "state=hover|size=medium (default)|style2=icon": __body31,
    // figma: State=Rest, Size=ExtraSmall, Style=Color
    "state=rest|size=extrasmall|style2=color": __body32,
    // figma: State=Rest, Size=ExtraSmall, Style=Image
    "state=rest|size=extrasmall|style2=image": __body33,
    // figma: State=Rest, Size=ExtraSmall, Style=Icon
    "state=rest|size=extrasmall|style2=icon": __body34,
    // figma: State=Rest, Size=Small, Style=Color
    "state=rest|size=sm|style2=color": __body35,
    // figma: State=Rest, Size=Small, Style=Image
    "state=rest|size=sm|style2=image": __body36,
    // figma: State=Rest, Size=Small, Style=Icon
    "state=rest|size=sm|style2=icon": __body37,
    // figma: State=Rest, Size=Medium (Default), Style=Color
    "state=rest|size=medium (default)|style2=color": __body38,
    // figma: State=Rest, Size=Medium (Default), Style=Image
    "state=rest|size=medium (default)|style2=image": __body39,
    // figma: State=Rest, Size=Medium (Default), Style=Icon
    "state=rest|size=medium (default)|style2=icon": __body40,
    // figma: State=Rest, Size=Large, Style=Color
    "state=rest|size=lg|style2=color": __body41,
    // figma: State=Rest, Size=Large, Style=Image
    "state=rest|size=lg|style2=image": __body42,
    // figma: State=Rest, Size=Large, Style=Icon
    "state=rest|size=lg|style2=icon": __body43,
    // figma: State=Transparent, Size=Medium (Default), Style=Color
    "state=transparent|size=medium (default)|style2=color": __body44,
    // figma: State=Transparent, Size=Small, Style=Color
    "state=transparent|size=sm|style2=color": __body45,
    // figma: State=Transparent, Size=ExtraSmall, Style=Color
    "state=transparent|size=extrasmall|style2=color": __body46,
    // figma: State=Transparent, Size=Large, Style=Color
    "state=transparent|size=lg|style2=color": __body47,
  };
  return (__impls[__vkey(props)] ?? __body32)();
}
export default Swatch;
