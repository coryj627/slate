import { __slateNorm } from '../internal/slateProps.jsx';
import { Radio } from './Radio.jsx';

// figma node: 9121:4066 RadioGroup (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "direction=" + __venc(p.direction);

export function RadioGroup(_p = {}) {
  _p = __slateNorm(_p, {"direction":["horizontal stacked","vertical","horizontal"]});
  const props = { ..._p, direction: _p.direction ?? "horizontal stacked" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        layout={"icon+label after"}
        checked={"off"}
        state={"rest"}
      />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <Radio layout={"icon+label below"} checked={"off"} state={"rest"} />}</div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <Radio layout={"icon+label below"} checked={"off"} state={"rest"} />}</div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon3 ?? <Radio layout={"icon+label below"} checked={"off"} state={"rest"} />}</div>
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon4 ?? <Radio layout={"icon+label below"} checked={"off"} state={"rest"} />}</div>
      <Radio
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        layout={"icon+label below"}
        checked={"off"}
        state={"rest"}
      />
    </div>
  );
  const __impls = {
    // figma: Direction=Vertical
    "direction=vertical": __body0,
    // figma: Direction=Horizontal
    "direction=horizontal": __body1,
    // figma: Direction=Horizontal stacked
    "direction=horizontal stacked": __body2,
  };
  return (__impls[__vkey(props)] ?? __body2)();
}
export default RadioGroup;
