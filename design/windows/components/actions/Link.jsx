import { __slateNorm } from '../internal/slateProps.jsx';
import { Open } from '../internal/Open.jsx';

// figma node: 9064:11414 Link (15 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state);

export function Link(_p = {}) {
  _p = __slateNorm(_p, {"style2":["default","subtle","overbrand"],"state":["rest/visited","hover","pressed","focused","disabled"]});
  const props = { ..._p, style2: _p.style2 ?? "default", state: _p.state ?? "rest/visited", showIcon: _p.showIcon ?? true, inlineStyle: _p.inlineStyle ?? false, text: _p.text ?? "Link" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--brand-foreground-link-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        {props.inlineStyle && (
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--brand-foreground-link-rest-3)",
          borderRight: "1px solid var(--brand-foreground-link-rest-3)",
          borderBottom: "1px solid var(--brand-foreground-link-rest-3)",
          borderLeft: "1px solid var(--brand-foreground-link-rest-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        )}
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--brand-foreground-link-rest-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--brand-foreground-link-hover-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--brand-foreground-link-hover-3)",
          borderRight: "1px solid var(--brand-foreground-link-hover-3)",
          borderBottom: "1px solid var(--brand-foreground-link-hover-3)",
          borderLeft: "1px solid var(--brand-foreground-link-hover-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--brand-foreground-link-hover-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--brand-foreground-link-pressed-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--brand-foreground-link-pressed-3)",
          borderRight: "1px solid var(--brand-foreground-link-pressed-3)",
          borderBottom: "1px solid var(--brand-foreground-link-pressed-3)",
          borderLeft: "1px solid var(--brand-foreground-link-pressed-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--brand-foreground-link-pressed-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--brand-foreground-link-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--brand-foreground-link-rest-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
        {props.inlineStyle && (
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-disabled-rest-4)",
          borderRight: "1px solid var(--neutral-foreground-disabled-rest-4)",
          borderBottom: "1px solid var(--neutral-foreground-disabled-rest-4)",
          borderLeft: "1px solid var(--neutral-foreground-disabled-rest-4)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        )}
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-link-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        {props.inlineStyle && (
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-2-rest-3)",
          borderRight: "1px solid var(--neutral-foreground-2-rest-3)",
          borderBottom: "1px solid var(--neutral-foreground-2-rest-3)",
          borderLeft: "1px solid var(--neutral-foreground-2-rest-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        )}
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-link-hover-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-2-link-hover-3)",
          borderRight: "1px solid var(--neutral-foreground-2-link-hover-3)",
          borderBottom: "1px solid var(--neutral-foreground-2-link-hover-3)",
          borderLeft: "1px solid var(--neutral-foreground-2-link-hover-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-hover-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"filled"} />}</div>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-link-pressed-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-2-link-pressed-3)",
          borderRight: "1px solid var(--neutral-foreground-2-link-pressed-3)",
          borderBottom: "1px solid var(--neutral-foreground-2-link-pressed-3)",
          borderLeft: "1px solid var(--neutral-foreground-2-link-pressed-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-pressed-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"filled"} />}</div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-link-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-2-rest-4)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-2-rest-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-inverted-link-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        {props.inlineStyle && (
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          borderRight: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          borderBottom: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          borderLeft: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
        )}
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-inverted-link-rest-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-inverted-link-hover-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-inverted-link-hover-3)",
          borderRight: "1px solid var(--neutral-foreground-inverted-link-hover-3)",
          borderBottom: "1px solid var(--neutral-foreground-inverted-link-hover-3)",
          borderLeft: "1px solid var(--neutral-foreground-inverted-link-hover-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-inverted-link-hover-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"filled"} />}</div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-inverted-link-pressed-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-inverted-link-pressed-3)",
          borderRight: "1px solid var(--neutral-foreground-inverted-link-pressed-3)",
          borderBottom: "1px solid var(--neutral-foreground-inverted-link-pressed-3)",
          borderLeft: "1px solid var(--neutral-foreground-inverted-link-pressed-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-inverted-link-pressed-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"filled"} />}</div>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 21,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-inverted-link-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
        <div style={{
          position: "relative",
          height: 3,
          borderTop: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          borderRight: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          borderBottom: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          borderLeft: "1px solid var(--neutral-foreground-inverted-link-rest-3)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showIcon && (
      <div style={{
          position: "relative",
          width: 20,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-inverted-link-rest-3)",
        }}>{props.icon1 ?? <Open size={"20"} theme={"regular"} />}</div>
      )}
    </div>
  );
  const __impls = {
    // figma: Style=Default, State=Rest/Visited
    "style2=default|state=rest/visited": __body0,
    // figma: Style=Default, State=Hover
    "style2=default|state=hover": __body1,
    // figma: Style=Default, State=Pressed
    "style2=default|state=pressed": __body2,
    // figma: Style=Default, State=Focused
    "style2=default|state=focused": __body3,
    // figma: Style=Default, State=Disabled
    "style2=default|state=disabled": __body4,
    // figma: Style=Subtle, State=Rest/Visited
    "style2=subtle|state=rest/visited": __body5,
    // figma: Style=Subtle, State=Hover
    "style2=subtle|state=hover": __body6,
    // figma: Style=Subtle, State=Pressed
    "style2=subtle|state=pressed": __body7,
    // figma: Style=Subtle, State=Focused
    "style2=subtle|state=focused": __body8,
    // figma: Style=Subtle, State=Disabled
    "style2=subtle|state=disabled": __body4,
    // figma: Style=OverBrand, State=Rest/Visited
    "style2=overbrand|state=rest/visited": __body9,
    // figma: Style=OverBrand, State=Hover
    "style2=overbrand|state=hover": __body10,
    // figma: Style=OverBrand, State=Pressed
    "style2=overbrand|state=pressed": __body11,
    // figma: Style=OverBrand, State=Focused
    "style2=overbrand|state=focused": __body12,
    // figma: Style=OverBrand, State=Disabled
    "style2=overbrand|state=disabled": __body4,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Link;
