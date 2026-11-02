import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9116:20807 SwatchPicker (14 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "layout=" + __venc(p.layout) + '|' + "spacing=" + __venc(p.spacing);

export function SwatchPicker(_p = {}) {
  _p = __slateNorm(_p, {"size":["extrasmall","sm","medium (default)","lg"],"layout":["grid","row"],"spacing":["md","sm"]});
  const props = { ..._p, size: _p.size ?? "extrasmall", layout: _p.layout ?? "grid", spacing: _p.spacing ?? "md" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 272,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 208,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        height: 20,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 240,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 304,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 226,
      height: 148,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        height: 24,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 258,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        height: 28,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 290,
      height: 188,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(242,242,242)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(127,127,127)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(208,206,206)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(213,220,228)",
          boxShadow: "inset 0 0 0 1px rgb(128,149,173)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(217,226,242)",
          boxShadow: "inset 0 0 0 1px rgb(116,149,207)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(251,228,214)",
          boxShadow: "inset 0 0 0 1px rgb(234,114,42)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,242,206)",
          boxShadow: "inset 0 0 0 1px rgb(190,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(226,239,218)",
          boxShadow: "inset 0 0 0 1px rgb(106,163,71)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(217,217,217)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(89,89,89)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(174,170,170)",
          boxShadow: "inset 0 0 0 1px rgb(152,147,147)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(172,185,201)",
          boxShadow: "inset 0 0 0 1px rgb(131,150,174)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(180,197,230)",
          boxShadow: "inset 0 0 0 1px rgb(117,148,209)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(247,203,174)",
          boxShadow: "inset 0 0 0 1px rgb(233,115,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,230,158)",
          boxShadow: "inset 0 0 0 1px rgb(190,141,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(196,224,181)",
          boxShadow: "inset 0 0 0 1px rgb(101,163,68)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(191,191,191)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(64,64,64)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(118,113,113)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(132,150,175)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(142,169,217)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(245,177,135)",
          boxShadow: "inset 0 0 0 1px rgb(237,112,36)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,218,111)",
          boxShadow: "inset 0 0 0 1px rgb(189,140,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(167,208,143)",
          boxShadow: "inset 0 0 0 1px rgb(104,164,69)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(166,166,166)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(38,38,38)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(59,56,56)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(50,62,78)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(47,83,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(197,91,34)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(191,144,32)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(82,129,57)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(128,128,128)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(13,13,13)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(23,23,23)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(34,42,53)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(31,55,99)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(132,61,21)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(128,97,18)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        height: 32,
        flexShrink: 0,
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(55,86,37)",
        }} />
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 272,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 304,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 240,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 208,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 20,
          height: 20,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 258,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 28,
          height: 28,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 290,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 32,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 226,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "10px 10px 10px 10px",
      alignItems: "flex-start",
      flexWrap: "wrap",
      alignContent: "space-between",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,255,255)",
          boxShadow: "inset 0 0 0 1px rgb(148,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(0,0,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(231,230,230)",
          boxShadow: "inset 0 0 0 1px rgb(152,148,148)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(68,84,105)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(68,113,193)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(238,127,61)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(255,193,46)",
          boxShadow: "inset 0 0 0 1px rgb(195,137,0)",
        }} />
      </div>
      <div style={{
        position: "relative",
        width: 24,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          backgroundColor: "rgb(110,173,76)",
        }} />
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=Medium (Default), Layout=Grid, Spacing=Medium
    "size=medium (default)|layout=grid|spacing=md": __body0,
    // figma: Size=ExtraSmall, Layout=Grid, Spacing=Medium
    "size=extrasmall|layout=grid|spacing=md": __body1,
    // figma: Size=Small, Layout=Grid, Spacing=Medium
    "size=sm|layout=grid|spacing=md": __body2,
    // figma: Size=Large, Layout=Grid, Spacing=Medium
    "size=lg|layout=grid|spacing=md": __body3,
    // figma: Size=Small, Layout=Grid, Spacing=Small
    "size=sm|layout=grid|spacing=sm": __body4,
    // figma: Size=Medium (Default), Layout=Grid, Spacing=Small
    "size=medium (default)|layout=grid|spacing=sm": __body5,
    // figma: Size=Large, Layout=Grid, Spacing=Small
    "size=lg|layout=grid|spacing=sm": __body6,
    // figma: Size=Medium (Default), Layout=Row, Spacing=Medium
    "size=medium (default)|layout=row|spacing=md": __body7,
    // figma: Size=Large, Layout=Row, Spacing=Medium
    "size=lg|layout=row|spacing=md": __body8,
    // figma: Size=Small, Layout=Row, Spacing=Medium
    "size=sm|layout=row|spacing=md": __body9,
    // figma: Size=ExtraSmall, Layout=Row, Spacing=Medium
    "size=extrasmall|layout=row|spacing=md": __body10,
    // figma: Size=Medium (Default), Layout=Row, Spacing=Small
    "size=medium (default)|layout=row|spacing=sm": __body11,
    // figma: Size=Large, Layout=Row, Spacing=Small
    "size=lg|layout=row|spacing=sm": __body12,
    // figma: Size=Small, Layout=Row, Spacing=Small
    "size=sm|layout=row|spacing=sm": __body13,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default SwatchPicker;
