import { __slateNorm } from './slateProps.jsx';
// figma node: 9064:4533 .Text (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size);

export function Text(_p = {}) {
  _p = __slateNorm(_p, {"size":["md","sm","lg"]});
  const props = { ..._p, size: _p.size ?? "md", placeholderText: _p.placeholderText ?? "Placeholder text", showPlaceholder: _p.showPlaceholder ?? true, enteredText: _p.enteredText ?? "Entered text", showEnteredText: _p.showEnteredText ?? false, showCursorAfterText: _p.showCursorAfterText ?? false, showCursorBeforeText: _p.showCursorBeforeText ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 140,
      height: 32,
      overflow: "hidden",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.showCursorBeforeText && (
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      )}
      {props.showPlaceholder && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-4-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.placeholderText}</span>
      )}
      {props.showEnteredText && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.enteredText}</span>
      )}
      {props.showCursorAfterText && (
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 140,
      height: 28,
      overflow: "hidden",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.showCursorBeforeText && (
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      )}
      {props.showPlaceholder && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-4-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.placeholderText}</span>
      )}
      {props.showEnteredText && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.enteredText}</span>
      )}
      {props.showCursorAfterText && (
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 140,
      height: 34,
      overflow: "hidden",
      display: "flex",
      flexDirection: "row",
      padding: "6px 2px 6px 2px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.showCursorBeforeText && (
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      )}
      {props.showPlaceholder && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-4-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.placeholderText}</span>
      )}
      {props.showEnteredText && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest-3)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.enteredText}</span>
      )}
      {props.showCursorAfterText && (
      <div style={{
        position: "relative",
        width: 1,
        backgroundColor: "var(--neutral-foreground-1-rest-3)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      )}
    </div>
  );
  const __impls = {
    // figma: Size=Medium
    "size=md": __body0,
    // figma: Size=Small
    "size=sm": __body1,
    // figma: Size=Large
    "size=lg": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Text;
