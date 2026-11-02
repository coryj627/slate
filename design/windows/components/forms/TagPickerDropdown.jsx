import { ArrowSwap } from '../internal/ArrowSwap.jsx';
import { TagPickerItem } from './TagPickerItem.jsx';

// figma node: 9064:15397 TagPicker/Dropdown (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "customLayout=" + __venc(p.customLayout);

export function TagPickerDropdown(_p = {}) {
  const props = { ..._p, customLayout: _p.customLayout ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "4px 4px 4px 4px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "8px 0px 8px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 94,
          height: 16,
          display: "flex",
          flexDirection: "row",
          padding: "0px 8px 0px 8px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>Section header</span>
        </div>
      </div>
      <TagPickerItem
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        state={"rest"}
        size={"medium (default)"}
      />
      <TagPickerItem
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        state={"rest"}
        size={"medium (default)"}
      />
      <TagPickerItem
        style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        state={"rest"}
        size={"medium (default)"}
      />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      padding: "4px 4px 4px 4px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-xs-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 182,
        overflow: "hidden",
        backgroundColor: "var(--brand-background-2-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-snudge) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <ArrowSwap
            style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest-4)",
            }}
            size={"16"}
            theme={"regular"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
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
  const __impls = {
    // figma: Custom layout=False
    "customLayout=false": __body0,
    // figma: Custom layout=True
    "customLayout=true": __body1,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default TagPickerDropdown;
