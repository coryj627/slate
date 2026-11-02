import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9380:3610 .Carousel play control (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state);

export function CarouselPlayControl(_p = {}) {
  _p = __slateNorm(_p, {"state":["pause","play"]});
  const props = { ..._p, state: _p.state ?? "pause" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      borderRadius: 4,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 2,
              width: 16,
              height: 16,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 Z M 8 1 C 4.134 1 1 4.134 1 8 C 1 11.866 4.134 15 8 15 C 11.866 15 15 11.866 15 8 C 15 4.134 11.866 1 8 1 Z M 6.5 4.5 C 6.776 4.5 7 4.724 7 5 L 7 11 C 7 11.276 6.776 11.5 6.5 11.5 C 6.224 11.5 6 11.276 6 11 L 6 5 C 6 4.724 6.224 4.5 6.5 4.5 Z M 9.5 4.5 C 9.776 4.5 10 4.724 10 5 L 10 11 C 10 11.276 9.776 11.5 9.5 11.5 C 9.224 11.5 9 11.276 9 11 L 9 5 C 9 4.724 9.224 4.5 9.5 4.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 24,
      height: 24,
      borderRadius: 4,
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={12.997} height={14} viewBox="0 0 12.997 14" fill="none" style={{
              position: "absolute",
              left: 5,
              top: 3,
              width: 12.997,
              height: 14,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 12.22 5.687 C 13.256 6.257 13.256 7.743 12.22 8.313 L 2.223 13.812 C 1.224 14.362 0 13.639 0 12.499 L 0 1.501 C 0 0.361 1.224 -0.362 2.223 0.188 L 12.22 5.687 Z M 11.738 7.438 C 12.083 7.248 12.083 6.752 11.738 6.562 L 1.741 1.063 C 1.408 0.88 1 1.121 1 1.501 L 1 12.499 C 1 12.879 1.408 13.12 1.741 12.937 L 11.738 7.438 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Pause
    "state=pause": __body0,
    // figma: State=Play
    "state=play": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default CarouselPlayControl;
