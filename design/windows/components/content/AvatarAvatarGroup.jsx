import { __slateNorm } from '../internal/slateProps.jsx';
import { AvatarAvatar3 } from '../internal/AvatarAvatar3.jsx';

// figma node: 9278:11479 Avatar/Avatar group (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout);

export function AvatarAvatarGroup(_p = {}) {
  _p = __slateNorm(_p, {"layout":["spread","stack"]});
  const props = { ..._p, layout: _p.layout ?? "spread" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spread) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <AvatarAvatar3 type={"image"} size={"32 (default)"} />}</div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 type={"icon"} size={"32 (default)"} />}</div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"EW"} type={"initials"} size={"32 (default)"} />}</div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon4 ?? <AvatarAvatar3 type={"initials"} size={"32 (default)"} />}</div>
      <AvatarAvatar3
        style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        initialsString2Max={"+5"}
        type={"initials"}
        size={"32 (default)"}
      />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--stack) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon1 ?? <AvatarAvatar3 type={"image"} size={"32 (default)"} />}</div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <AvatarAvatar3 type={"icon"} size={"32 (default)"} />}</div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon3 ?? <AvatarAvatar3 initialsString2Max={"EW"} type={"initials"} size={"32 (default)"} />}</div>
      <div style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon4 ?? <AvatarAvatar3 type={"initials"} size={"32 (default)"} />}</div>
      <AvatarAvatar3
        style={{
          position: "relative",
          width: 32,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}
        initialsString2Max={"+5"}
        type={"initials"}
        size={"32 (default)"}
      />
    </div>
  );
  const __impls = {
    // figma: Layout=Spread
    "layout=spread": __body0,
    // figma: Layout=Stack
    "layout=stack": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default AvatarAvatarGroup;
