import { __slateNorm } from './slateProps.jsx';
import { Beaker } from './Beaker.jsx';
import { Bot } from './Bot.jsx';
import { CircleHint } from './CircleHint.jsx';
import { Copilot } from './Copilot.jsx';
import { Glasses } from './Glasses.jsx';
import { WrenchScrewdriver } from './WrenchScrewdriver.jsx';

// figma node: 9000:367 .State tags (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state);

export function StateTags(_p = {}) {
  _p = __slateNorm(_p, {"state":["visionary feature","experimental feature","establishing pattern","coherent pattern","local feature","incoherent feature"]});
  const props = { ..._p, state: _p.state ?? "visionary feature" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 26,
      backgroundColor: "rgb(238,230,247)",
      display: "flex",
      flexDirection: "row",
      gap: 6,
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <Glasses size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "rgb(131,62,222)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Visionary feature"}</span>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 26,
      backgroundColor: "rgb(228,232,254)",
      display: "flex",
      flexDirection: "row",
      gap: 6,
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <Beaker size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "rgb(13,52,255)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Experimental feature"}</span>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 26,
      backgroundColor: "rgb(222,250,243)",
      display: "flex",
      flexDirection: "row",
      gap: 6,
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <WrenchScrewdriver size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "rgb(22,123,99)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Establishing pattern"}</span>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 26,
      backgroundColor: "rgb(222,250,226)",
      display: "flex",
      flexDirection: "row",
      gap: 6,
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <Copilot size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "rgb(22,123,99)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Coherent pattern"}</span>
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 26,
      backgroundColor: "rgb(252,231,254)",
      display: "flex",
      flexDirection: "row",
      gap: 6,
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <Bot size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "rgb(192,11,174)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Local feature"}</span>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 26,
      backgroundColor: "rgb(254,231,228)",
      display: "flex",
      flexDirection: "row",
      gap: 6,
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <CircleHint size={"20"} theme={"regular"} />}</div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "rgb(196,50,17)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "Incoherent feature"}</span>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Visionary feature
    "state=visionary feature": __body0,
    // figma: State=Experimental feature
    "state=experimental feature": __body1,
    // figma: State=Establishing pattern
    "state=establishing pattern": __body2,
    // figma: State=Coherent pattern
    "state=coherent pattern": __body3,
    // figma: State=Local feature
    "state=local feature": __body4,
    // figma: State=Incoherent feature
    "state=incoherent feature": __body5,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default StateTags;
