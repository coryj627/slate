import { Chevron } from './Chevron.jsx';

// figma node: 9291:3752 .Chevron (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "expand=" + __venc(p.expand);

export function Chevron5(_p = {}) {
  const props = { ..._p, expand: _p.expand ?? false };
  const __body0 = () => (
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
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Chevron direction={"up"} size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
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
          color: "var(--neutral-foreground-2-rest)",
        }}>{props.icon1 ?? <Chevron direction={"down"} size={"20"} theme={"regular"} />}</div>
    </div>
  );
  const __impls = {
    // figma: Expand=True
    "expand=true": __body0,
    // figma: Expand=False
    "expand=false": __body1,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default Chevron5;
