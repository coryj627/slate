import { ArrowSwap2 } from '../internal/ArrowSwap2.jsx';
import { ListItem } from './ListItem.jsx';

// figma node: 9159:2641 List content (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "swappableContent=" + __venc(p.swappableContent);

export function ListContent(_p = {}) {
  const props = { ..._p, swappableContent: _p.swappableContent ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 312,
      height: 210,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      gap: 10,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        backgroundColor: "var(--brand-background-2-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ArrowSwap2
            style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}
            size={"16"}
            theme={"regular"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 0px 2px 0px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              textAlign: "center",
              lineHeight: "14px",
              color: "var(--brand-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>SWAP AREA WITH YOUR COMPONENT</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 312,
      overflow: "hidden",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-list-section-gap) * 1px)",
        padding: "4px 4px 4px 4px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <ListItem
          style={{
            position: "relative",
            height: 32,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          state={"rest"}
          type={"header"}
        />
        <ListItem
          style={{
            position: "relative",
            height: 32,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          checkmark={false}
          state={"rest"}
          type={"single select"}
        />
        <ListItem
          style={{
            position: "relative",
            height: 32,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          checkmark={false}
          state={"rest"}
          type={"single select"}
        />
        <ListItem
          style={{
            position: "relative",
            height: 32,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          checkmark={false}
          state={"rest"}
          type={"single select"}
        />
        <ListItem
          style={{
            position: "relative",
            height: 32,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          checkmark={false}
          state={"rest"}
          type={"single select"}
        />
        <ListItem
          style={{
            position: "relative",
            height: 32,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}
          checkmark={false}
          state={"rest"}
          type={"single select"}
        />
      </div>
    </div>
  );
  const __impls = {
    // figma: Swappable content=True
    "swappableContent=true": __body0,
    // figma: Swappable content=False
    "swappableContent=false": __body1,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default ListContent;
