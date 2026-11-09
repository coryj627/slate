import { __slateNorm } from '../internal/slateProps.jsx';
import { ImageSwapPlaceholder } from '../internal/ImageSwapPlaceholder.jsx';

// figma node: 9026:2278 Compound button (75 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size);

export function CompoundButton(_p = {}) {
  _p = __slateNorm(_p, {"style2":["primary","secondary (default)","outline","subtle","transparent"],"state":["rest","hover","pressed","selected","disabled"],"size":["medium (default)","sm","lg"]});
  const props = { ..._p, image: _p.image ?? true, secondaryText: _p.secondaryText ?? true, primaryString: _p.primaryString ?? "Primary string", secondaryString: _p.secondaryString ?? "Secondary string", style2: _p.style2 ?? "primary", state: _p.state ?? "rest", size: _p.size ?? "medium (default)", focus: _p.focus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 179,
        height: 72,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={179} height={72} viewBox="0 0 179 72" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 179,
          height: 72,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 175 1 L 175 0 L 175 -1 L 4 -1 L 4 0 Z M 179 4 L 178 4 L 178 68 L 179 68 L 180 68 L 180 4 L 179 4 Z M 175 72 L 175 71 L 4 71 L 4 72 L 4 73 L 175 73 L 175 72 Z M 0 68 L 1 68 L 1 4 L 0 4 L -1 4 L -1 68 L 0 68 Z M 4 72 L 4 71 C 2.343 71 1 69.657 1 68 L 0 68 L -1 68 C -1 70.761 1.239 73 4 73 L 4 72 Z M 179 68 L 178 68 C 178 69.657 176.657 71 175 71 L 175 72 L 175 73 C 177.761 73 180 70.761 180 68 L 179 68 Z M 175 0 L 175 1 C 176.657 1 178 2.343 178 4 L 179 4 L 180 4 C 180 1.239 177.761 -1 175 -1 L 175 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "2px solid var(--neutral-stroke-1-selected)",
      borderRight: "2px solid var(--neutral-stroke-1-selected)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-l) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l-2) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-l) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 179,
        height: 72,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={179} height={72} viewBox="0 0 179 72" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 179,
          height: 72,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 175 1 L 175 0 L 175 -1 L 4 -1 L 4 0 Z M 179 4 L 178 4 L 178 68 L 179 68 L 180 68 L 180 4 L 179 4 Z M 175 72 L 175 71 L 4 71 L 4 72 L 4 73 L 175 73 L 175 72 Z M 0 68 L 1 68 L 1 4 L 0 4 L -1 4 L -1 68 L 0 68 Z M 4 72 L 4 71 C 2.343 71 1 69.657 1 68 L 0 68 L -1 68 C -1 70.761 1.239 73 4 73 L 4 72 Z M 179 68 L 178 68 C 178 69.657 176.657 71 175 71 L 175 72 L 175 73 C 177.761 73 180 70.761 180 68 L 179 68 Z M 175 0 L 175 1 C 176.657 1 178 2.343 178 4 L 179 4 L 180 4 C 180 1.239 177.761 -1 175 -1 L 175 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 179,
        height: 72,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={179} height={72} viewBox="0 0 179 72" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 179,
          height: 72,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 175 1 L 175 0 L 175 -1 L 4 -1 L 4 0 Z M 179 4 L 178 4 L 178 68 L 179 68 L 180 68 L 180 4 L 179 4 Z M 175 72 L 175 71 L 4 71 L 4 72 L 4 73 L 175 73 L 175 72 Z M 0 68 L 1 68 L 1 4 L 0 4 L -1 4 L -1 68 L 0 68 Z M 4 72 L 4 71 C 2.343 71 1 69.657 1 68 L 0 68 L -1 68 C -1 70.761 1.239 73 4 73 L 4 72 Z M 179 68 L 178 68 C 178 69.657 176.657 71 175 71 L 175 72 L 175 73 C 177.761 73 180 70.761 180 68 L 179 68 Z M 175 0 L 175 1 C 176.657 1 178 2.343 178 4 L 179 4 L 180 4 C 180 1.239 177.761 -1 175 -1 L 175 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-l-2) * 1px)",
      padding: "16px 16px 16px 16px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingTop: "calc(var(--vertical-l) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
      paddingBottom: "calc(var(--vertical-l) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 179,
        height: 72,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={179} height={72} viewBox="0 0 179 72" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 179,
          height: 72,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 175 1 L 175 0 L 175 -1 L 4 -1 L 4 0 Z M 179 4 L 178 4 L 178 68 L 179 68 L 180 68 L 180 4 L 179 4 Z M 175 72 L 175 71 L 4 71 L 4 72 L 4 73 L 175 73 L 175 72 Z M 0 68 L 1 68 L 1 4 L 0 4 L -1 4 L -1 68 L 0 68 Z M 4 72 L 4 71 C 2.343 71 1 69.657 1 68 L 0 68 L -1 68 C -1 70.761 1.239 73 4 73 L 4 72 Z M 179 68 L 178 68 C 178 69.657 176.657 71 175 71 L 175 72 L 175 73 C 177.761 73 180 70.761 180 68 L 179 68 Z M 175 0 L 175 1 C 176.657 1 178 2.343 178 4 L 179 4 L 180 4 C 180 1.239 177.761 -1 175 -1 L 175 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 155,
        height: 56,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={155} height={56} viewBox="0 0 155 56" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 155,
          height: 56,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 151 1 L 151 0 L 151 -1 L 4 -1 L 4 0 Z M 155 4 L 154 4 L 154 52 L 155 52 L 156 52 L 156 4 L 155 4 Z M 151 56 L 151 55 L 4 55 L 4 56 L 4 57 L 151 57 L 151 56 Z M 0 52 L 1 52 L 1 4 L 0 4 L -1 4 L -1 52 L 0 52 Z M 4 56 L 4 55 C 2.343 55 1 53.657 1 52 L 0 52 L -1 52 C -1 54.761 1.239 57 4 57 L 4 56 Z M 155 52 L 154 52 C 154 53.657 152.657 55 151 55 L 151 56 L 151 57 C 153.761 57 156 54.761 156 52 L 155 52 Z M 151 0 L 151 1 C 152.657 1 154 2.343 154 4 L 155 4 L 156 4 C 156 1.239 153.761 -1 151 -1 L 151 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "2px solid var(--neutral-stroke-1-selected)",
      borderRight: "2px solid var(--neutral-stroke-1-selected)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s-2) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 155,
        height: 56,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={155} height={56} viewBox="0 0 155 56" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 155,
          height: 56,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 151 1 L 151 0 L 151 -1 L 4 -1 L 4 0 Z M 155 4 L 154 4 L 154 52 L 155 52 L 156 52 L 156 4 L 155 4 Z M 151 56 L 151 55 L 4 55 L 4 56 L 4 57 L 151 57 L 151 56 Z M 0 52 L 1 52 L 1 4 L 0 4 L -1 4 L -1 52 L 0 52 Z M 4 56 L 4 55 C 2.343 55 1 53.657 1 52 L 0 52 L -1 52 C -1 54.761 1.239 57 4 57 L 4 56 Z M 155 52 L 154 52 C 154 53.657 152.657 55 151 55 L 151 56 L 151 57 C 153.761 57 156 54.761 156 52 L 155 52 Z M 151 0 L 151 1 C 152.657 1 154 2.343 154 4 L 155 4 L 156 4 C 156 1.239 153.761 -1 151 -1 L 151 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 155,
        height: 56,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={155} height={56} viewBox="0 0 155 56" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 155,
          height: 56,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 151 1 L 151 0 L 151 -1 L 4 -1 L 4 0 Z M 155 4 L 154 4 L 154 52 L 155 52 L 156 52 L 156 4 L 155 4 Z M 151 56 L 151 55 L 4 55 L 4 56 L 4 57 L 151 57 L 151 56 Z M 0 52 L 1 52 L 1 4 L 0 4 L -1 4 L -1 52 L 0 52 Z M 4 56 L 4 55 C 2.343 55 1 53.657 1 52 L 0 52 L -1 52 C -1 54.761 1.239 57 4 57 L 4 56 Z M 155 52 L 154 52 C 154 53.657 152.657 55 151 55 L 151 56 L 151 57 C 153.761 57 156 54.761 156 52 L 155 52 Z M 151 0 L 151 1 C 152.657 1 154 2.343 154 4 L 155 4 L 156 4 C 156 1.239 153.761 -1 151 -1 L 151 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "8px 8px 8px 8px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 155,
        height: 56,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={155} height={56} viewBox="0 0 155 56" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 155,
          height: 56,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 151 1 L 151 0 L 151 -1 L 4 -1 L 4 0 Z M 155 4 L 154 4 L 154 52 L 155 52 L 156 52 L 156 4 L 155 4 Z M 151 56 L 151 55 L 4 55 L 4 56 L 4 57 L 151 57 L 151 56 Z M 0 52 L 1 52 L 1 4 L 0 4 L -1 4 L -1 52 L 0 52 Z M 4 56 L 4 55 C 2.343 55 1 53.657 1 52 L 0 52 L -1 52 C -1 54.761 1.239 57 4 57 L 4 56 Z M 155 52 L 154 52 C 154 53.657 152.657 55 151 55 L 151 56 L 151 57 C 153.761 57 156 54.761 156 52 L 155 52 Z M 151 0 L 151 1 C 152.657 1 154 2.343 154 4 L 155 4 L 156 4 C 156 1.239 153.761 -1 151 -1 L 151 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-2-brand-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-brand-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 167,
        height: 64,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={167} height={64} viewBox="0 0 167 64" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 167,
          height: 64,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 163 1 L 163 0 L 163 -1 L 4 -1 L 4 0 Z M 167 4 L 166 4 L 166 60 L 167 60 L 168 60 L 168 4 L 167 4 Z M 163 64 L 163 63 L 4 63 L 4 64 L 4 65 L 163 65 L 163 64 Z M 0 60 L 1 60 L 1 4 L 0 4 L -1 4 L -1 60 L 0 60 Z M 4 64 L 4 63 C 2.343 63 1 61.657 1 60 L 0 60 L -1 60 C -1 62.761 1.239 65 4 65 L 4 64 Z M 167 60 L 166 60 C 166 61.657 164.657 63 163 63 L 163 64 L 163 65 C 165.761 65 168 62.761 168 60 L 167 60 Z M 163 0 L 163 1 C 164.657 1 166 2.343 166 4 L 167 4 L 168 4 C 168 1.239 165.761 -1 163 -1 L 163 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "2px solid var(--neutral-stroke-1-selected)",
      borderRight: "2px solid var(--neutral-stroke-1-selected)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-m) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-m) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 167,
        height: 64,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={167} height={64} viewBox="0 0 167 64" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 167,
          height: 64,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 163 1 L 163 0 L 163 -1 L 4 -1 L 4 0 Z M 167 4 L 166 4 L 166 60 L 167 60 L 168 60 L 168 4 L 167 4 Z M 163 64 L 163 63 L 4 63 L 4 64 L 4 65 L 163 65 L 163 64 Z M 0 60 L 1 60 L 1 4 L 0 4 L -1 4 L -1 60 L 0 60 Z M 4 64 L 4 63 C 2.343 63 1 61.657 1 60 L 0 60 L -1 60 C -1 62.761 1.239 65 4 65 L 4 64 Z M 167 60 L 166 60 C 166 61.657 164.657 63 163 63 L 163 64 L 163 65 C 165.761 65 168 62.761 168 60 L 167 60 Z M 163 0 L 163 1 C 164.657 1 166 2.343 166 4 L 167 4 L 168 4 C 168 1.239 165.761 -1 163 -1 L 163 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 167,
        height: 64,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={167} height={64} viewBox="0 0 167 64" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 167,
          height: 64,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 163 1 L 163 0 L 163 -1 L 4 -1 L 4 0 Z M 167 4 L 166 4 L 166 60 L 167 60 L 168 60 L 168 4 L 167 4 Z M 163 64 L 163 63 L 4 63 L 4 64 L 4 65 L 163 65 L 163 64 Z M 0 60 L 1 60 L 1 4 L 0 4 L -1 4 L -1 60 L 0 60 Z M 4 64 L 4 63 C 2.343 63 1 61.657 1 60 L 0 60 L -1 60 C -1 62.761 1.239 65 4 65 L 4 64 Z M 167 60 L 166 60 C 166 61.657 164.657 63 163 63 L 163 64 L 163 65 C 165.761 65 168 62.761 168 60 L 167 60 Z M 163 0 L 163 1 C 164.657 1 166 2.343 166 4 L 167 4 L 168 4 C 168 1.239 165.761 -1 163 -1 L 163 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-disabled-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected)",
      borderTop: "1px solid var(--neutral-stroke-1-selected)",
      borderRight: "1px solid var(--neutral-stroke-1-selected)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-selected)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      borderTop: "1px solid var(--neutral-stroke-1-hover)",
      borderRight: "1px solid var(--neutral-stroke-1-hover)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid var(--neutral-stroke-1-rest)",
      borderRight: "1px solid var(--neutral-stroke-1-rest)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      padding: "12px 12px 12px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--vertical-m) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--vertical-m) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 167,
        height: 64,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
      }} />
      )}
      {props.image && (
      <div style={{
          position: "relative",
          width: 40,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.swap40x40Img ?? <ImageSwapPlaceholder />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.secondaryText && (
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-2-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.secondaryString}</span>
        )}
      </div>
    </div>
  );
  const __impls = {
    // figma: Style=Transparent, State=Disabled, Size=Large
    "style2=transparent|state=disabled|size=lg": __body0,
    // figma: Style=Transparent, State=Selected, Size=Large
    "style2=transparent|state=selected|size=lg": __body1,
    // figma: Style=Transparent, State=Pressed, Size=Large
    "style2=transparent|state=pressed|size=lg": __body2,
    // figma: Style=Transparent, State=Hover, Size=Large
    "style2=transparent|state=hover|size=lg": __body3,
    // figma: Style=Transparent, State=Rest, Size=Large
    "style2=transparent|state=rest|size=lg": __body4,
    // figma: Style=Subtle, State=Disabled, Size=Large
    "style2=subtle|state=disabled|size=lg": __body5,
    // figma: Style=Subtle, State=Selected, Size=Large
    "style2=subtle|state=selected|size=lg": __body6,
    // figma: Style=Subtle, State=Pressed, Size=Large
    "style2=subtle|state=pressed|size=lg": __body7,
    // figma: Style=Subtle, State=Hover, Size=Large
    "style2=subtle|state=hover|size=lg": __body8,
    // figma: Style=Subtle, State=Rest, Size=Large
    "style2=subtle|state=rest|size=lg": __body4,
    // figma: Style=Outline, State=Disabled, Size=Large
    "style2=outline|state=disabled|size=lg": __body9,
    // figma: Style=Outline, State=Selected, Size=Large
    "style2=outline|state=selected|size=lg": __body10,
    // figma: Style=Outline, State=Pressed, Size=Large
    "style2=outline|state=pressed|size=lg": __body11,
    // figma: Style=Outline, State=Hover, Size=Large
    "style2=outline|state=hover|size=lg": __body12,
    // figma: Style=Outline, State=Rest, Size=Large
    "style2=outline|state=rest|size=lg": __body13,
    // figma: Style=Primary, State=Disabled, Size=Large
    "style2=primary|state=disabled|size=lg": __body5,
    // figma: Style=Primary, State=Selected, Size=Large
    "style2=primary|state=selected|size=lg": __body14,
    // figma: Style=Primary, State=Pressed, Size=Large
    "style2=primary|state=pressed|size=lg": __body15,
    // figma: Style=Primary, State=Hover, Size=Large
    "style2=primary|state=hover|size=lg": __body16,
    // figma: Style=Primary, State=Rest, Size=Large
    "style2=primary|state=rest|size=lg": __body17,
    // figma: Style=Secondary (Default), State=Disabled, Size=Large
    "style2=secondary (default)|state=disabled|size=lg": __body18,
    // figma: Style=Secondary (Default), State=Selected, Size=Large
    "style2=secondary (default)|state=selected|size=lg": __body19,
    // figma: Style=Secondary (Default), State=Pressed, Size=Large
    "style2=secondary (default)|state=pressed|size=lg": __body20,
    // figma: Style=Secondary (Default), State=Hover, Size=Large
    "style2=secondary (default)|state=hover|size=lg": __body21,
    // figma: Style=Secondary (Default), State=Rest, Size=Large
    "style2=secondary (default)|state=rest|size=lg": __body22,
    // figma: Style=Transparent, State=Disabled, Size=Small
    "style2=transparent|state=disabled|size=sm": __body23,
    // figma: Style=Transparent, State=Selected, Size=Small
    "style2=transparent|state=selected|size=sm": __body24,
    // figma: Style=Transparent, State=Pressed, Size=Small
    "style2=transparent|state=pressed|size=sm": __body25,
    // figma: Style=Transparent, State=Hover, Size=Small
    "style2=transparent|state=hover|size=sm": __body26,
    // figma: Style=Transparent, State=Rest, Size=Small
    "style2=transparent|state=rest|size=sm": __body27,
    // figma: Style=Subtle, State=Disabled, Size=Small
    "style2=subtle|state=disabled|size=sm": __body28,
    // figma: Style=Subtle, State=Selected, Size=Small
    "style2=subtle|state=selected|size=sm": __body29,
    // figma: Style=Subtle, State=Pressed, Size=Small
    "style2=subtle|state=pressed|size=sm": __body30,
    // figma: Style=Subtle, State=Hover, Size=Small
    "style2=subtle|state=hover|size=sm": __body31,
    // figma: Style=Subtle, State=Rest, Size=Small
    "style2=subtle|state=rest|size=sm": __body27,
    // figma: Style=Outline, State=Disabled, Size=Small
    "style2=outline|state=disabled|size=sm": __body32,
    // figma: Style=Outline, State=Selected, Size=Small
    "style2=outline|state=selected|size=sm": __body33,
    // figma: Style=Outline, State=Pressed, Size=Small
    "style2=outline|state=pressed|size=sm": __body34,
    // figma: Style=Outline, State=Hover, Size=Small
    "style2=outline|state=hover|size=sm": __body35,
    // figma: Style=Outline, State=Rest, Size=Small
    "style2=outline|state=rest|size=sm": __body36,
    // figma: Style=Primary, State=Disabled, Size=Small
    "style2=primary|state=disabled|size=sm": __body28,
    // figma: Style=Primary, State=Selected, Size=Small
    "style2=primary|state=selected|size=sm": __body37,
    // figma: Style=Primary, State=Pressed, Size=Small
    "style2=primary|state=pressed|size=sm": __body38,
    // figma: Style=Primary, State=Hover, Size=Small
    "style2=primary|state=hover|size=sm": __body39,
    // figma: Style=Primary, State=Rest, Size=Small
    "style2=primary|state=rest|size=sm": __body40,
    // figma: Style=Secondary (Default), State=Disabled, Size=Small
    "style2=secondary (default)|state=disabled|size=sm": __body41,
    // figma: Style=Secondary (Default), State=Selected, Size=Small
    "style2=secondary (default)|state=selected|size=sm": __body42,
    // figma: Style=Secondary (Default), State=Pressed, Size=Small
    "style2=secondary (default)|state=pressed|size=sm": __body43,
    // figma: Style=Secondary (Default), State=Hover, Size=Small
    "style2=secondary (default)|state=hover|size=sm": __body44,
    // figma: Style=Secondary (Default), State=Rest, Size=Small
    "style2=secondary (default)|state=rest|size=sm": __body45,
    // figma: Style=Transparent, State=Disabled, Size=Medium (Default)
    "style2=transparent|state=disabled|size=medium (default)": __body46,
    // figma: Style=Transparent, State=Selected, Size=Medium (Default)
    "style2=transparent|state=selected|size=medium (default)": __body47,
    // figma: Style=Transparent, State=Pressed, Size=Medium (Default)
    "style2=transparent|state=pressed|size=medium (default)": __body48,
    // figma: Style=Transparent, State=Hover, Size=Medium (Default)
    "style2=transparent|state=hover|size=medium (default)": __body49,
    // figma: Style=Transparent, State=Rest, Size=Medium (Default)
    "style2=transparent|state=rest|size=medium (default)": __body50,
    // figma: Style=Subtle, State=Disabled, Size=Medium (Default)
    "style2=subtle|state=disabled|size=medium (default)": __body51,
    // figma: Style=Subtle, State=Selected, Size=Medium (Default)
    "style2=subtle|state=selected|size=medium (default)": __body52,
    // figma: Style=Subtle, State=Pressed, Size=Medium (Default)
    "style2=subtle|state=pressed|size=medium (default)": __body53,
    // figma: Style=Subtle, State=Hover, Size=Medium (Default)
    "style2=subtle|state=hover|size=medium (default)": __body54,
    // figma: Style=Subtle, State=Rest, Size=Medium (Default)
    "style2=subtle|state=rest|size=medium (default)": __body50,
    // figma: Style=Outline, State=Disabled, Size=Medium (Default)
    "style2=outline|state=disabled|size=medium (default)": __body55,
    // figma: Style=Outline, State=Selected, Size=Medium (Default)
    "style2=outline|state=selected|size=medium (default)": __body56,
    // figma: Style=Outline, State=Pressed, Size=Medium (Default)
    "style2=outline|state=pressed|size=medium (default)": __body57,
    // figma: Style=Outline, State=Hover, Size=Medium (Default)
    "style2=outline|state=hover|size=medium (default)": __body58,
    // figma: Style=Outline, State=Rest, Size=Medium (Default)
    "style2=outline|state=rest|size=medium (default)": __body59,
    // figma: Style=Primary, State=Disabled, Size=Medium (Default)
    "style2=primary|state=disabled|size=medium (default)": __body51,
    // figma: Style=Primary, State=Selected, Size=Medium (Default)
    "style2=primary|state=selected|size=medium (default)": __body60,
    // figma: Style=Primary, State=Pressed, Size=Medium (Default)
    "style2=primary|state=pressed|size=medium (default)": __body61,
    // figma: Style=Primary, State=Hover, Size=Medium (Default)
    "style2=primary|state=hover|size=medium (default)": __body62,
    // figma: Style=Primary, State=Rest, Size=Medium (Default)
    "style2=primary|state=rest|size=medium (default)": __body63,
    // figma: Style=Secondary (Default), State=Disabled, Size=Medium (Default)
    "style2=secondary (default)|state=disabled|size=medium (default)": __body64,
    // figma: Style=Secondary (Default), State=Selected, Size=Medium (Default)
    "style2=secondary (default)|state=selected|size=medium (default)": __body65,
    // figma: Style=Secondary (Default), State=Pressed, Size=Medium (Default)
    "style2=secondary (default)|state=pressed|size=medium (default)": __body66,
    // figma: Style=Secondary (Default), State=Hover, Size=Medium (Default)
    "style2=secondary (default)|state=hover|size=medium (default)": __body67,
    // figma: Style=Secondary (Default), State=Rest, Size=Medium (Default)
    "style2=secondary (default)|state=rest|size=medium (default)": __body68,
  };
  return (__impls[__vkey(props)] ?? __body63)();
}
export default CompoundButton;
