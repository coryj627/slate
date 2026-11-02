import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9114:811 Material acrylic (1 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "material=" + __venc(p.material);

export function MaterialAcrylic(_p = {}) {
  _p = __slateNorm(_p, {"material":["acrylic"]});
  const props = { ..._p, material: _p.material ?? "acrylic" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 282,
      height: 76,
      background: "linear-gradient(rgba(255,255,255,0.5),rgba(255,255,255,0.5)), linear-gradient(rgba(231,236,243,0.7),rgba(231,236,243,0.7))",
      backdropFilter: "blur(60px)",
      display: "flex",
      flexDirection: "column",
      padding: "16px 16px 16px 16px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--spacing-horizontal-l-2) * 1px)",
      position: "relative",
      ...props.style,
    }} />
  );
  const __impls = {
    // figma: Material=Acrylic
    "material=acrylic": __body0,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default MaterialAcrylic;
