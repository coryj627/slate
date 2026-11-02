import { __slateNorm } from '../internal/slateProps.jsx';
import { Popover } from './Popover.jsx';
import { TeachingPopoverContent } from '../internal/TeachingPopoverContent.jsx';
import { TeachingPopoverContentOnBrand } from '../internal/TeachingPopoverContentOnBrand.jsx';

// figma node: 9322:1127 Teaching Popover (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2);

export function TeachingPopover(_p = {}) {
  _p = __slateNorm(_p, {"style2":["default","brand"]});
  const props = { ..._p, style2: _p.style2 ?? "default" };
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
      <Popover
        style={{
          position: "relative",
          height: 286,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        contentSwap={<TeachingPopoverContent />}
        style2={"default"}
      />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <Popover
        style={{
          position: "relative",
          height: 286,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        contentSwap={<TeachingPopoverContentOnBrand />}
        style2={"brand"}
      />
    </div>
  );
  const __impls = {
    // figma: Style=Default
    "style2=default": __body0,
    // figma: Style=Brand
    "style2=brand": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default TeachingPopover;
