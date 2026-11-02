import { __slateNorm } from './slateProps.jsx';
// figma node: 9738:67 Blocks / Text / Headline (4 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2);

export function BlocksTextHeadline(_p = {}) {
  _p = __slateNorm(_p, {"style2":["xxl headline","xl headline","l headline","m headline"]});
  const props = { ..._p, preview: _p.preview ?? false, style2: _p.style2 ?? "xxl headline" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 672,
      backgroundColor: "var(--colors-neutral-grey-white)",
      display: "flex",
      flexDirection: "column",
      gap: 32,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 16,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 700,
          fontSize: 56,
          whiteSpace: "nowrap",
          lineHeight: "80px",
          letterSpacing: "-0.040em",
          color: "var(--colors-neutral-grey-black)",
          flexShrink: 0,
        }}>{props.text1 ?? "XXL Headline 56"}</span>
        {props.preview && (
        <div style={{
          position: "relative",
          height: 48,
          display: "flex",
          flexDirection: "column",
          padding: "16px 0px 16px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            height: 32,
            borderRadius: 23,
            boxShadow: "inset 0 0 0 1px var(--colors-neutral-grey-black)",
            display: "flex",
            flexDirection: "row",
            gap: 12,
            padding: "0px 12px 0px 12px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 700,
              fontSize: 14,
              textAlign: "justify",
              whiteSpace: "nowrap",
              lineHeight: "24px",
              textBox: "trim-both cap alphabetic",
              letterSpacing: "-0.025em",
              color: "var(--colors-neutral-grey-black)",
              flexShrink: 0,
            }}>{props.text2 ?? "Preview"}</span>
          </div>
        </div>
        )}
      </div>
      <span style={{
        position: "relative",
        width: 480,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 18,
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
      }}>{props.text3 ?? "Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI."}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 672,
      backgroundColor: "var(--colors-neutral-grey-white)",
      display: "flex",
      flexDirection: "column",
      gap: 16,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 28,
        lineHeight: "100%",
        letterSpacing: "-0.040em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
        alignSelf: "stretch",
        whiteSpace: "nowrap",
      }}>{props.text1 ?? "XL Headline 28"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI. Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI. "}</span>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 672,
      backgroundColor: "var(--colors-neutral-grey-white)",
      display: "flex",
      flexDirection: "column",
      gap: 16,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 24,
        lineHeight: "100%",
        letterSpacing: "-0.040em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
        alignSelf: "stretch",
        whiteSpace: "nowrap",
      }}>{props.text1 ?? "L Headline 24"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI. Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI. "}</span>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 672,
      backgroundColor: "var(--colors-neutral-grey-white)",
      display: "flex",
      flexDirection: "column",
      gap: 16,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 20,
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
        alignSelf: "stretch",
        whiteSpace: "nowrap",
      }}>{props.text1 ?? "M Headine 20"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--colors-neutral-grey-black)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI. Fluent UI's robust, up-to-date controls are built with the React framework etc. Look through the control list to see the building blocks that are available using Fluent UI. "}</span>
    </div>
  );
  const __impls = {
    // figma: Style=XXL Headline
    "style2=xxl headline": __body0,
    // figma: Style=XL Headline
    "style2=xl headline": __body1,
    // figma: Style=L Headline
    "style2=l headline": __body2,
    // figma: Style=M Headline
    "style2=m headline": __body3,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default BlocksTextHeadline;
