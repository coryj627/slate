import { __slateNorm } from '../internal/slateProps.jsx';
import { Checkmark2 } from '../internal/Checkmark2.jsx';
import { Image } from '../internal/Image.jsx';
import { ItemSplitButton } from '../internal/ItemSplitButton.jsx';

// figma node: 9121:6401 Menu split item (7 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "sectionHeader=" + __venc(p.sectionHeader) + '|' + "state=" + __venc(p.state);

export function MenuSplitItem(_p = {}) {
  _p = __slateNorm(_p, {"sectionHeader":["false"],"state":["rest","rest (checked only)","hover","pressed","selected","disabled","disabled (checked only)"]});
  const props = { ..._p, checkmark: _p.checkmark ?? false, selected: _p.selected ?? true, sectionHeader: _p.sectionHeader ?? "false", state: _p.state ?? "rest", endContent: _p.endContent ?? true, endContentString: _p.endContentString ?? "Shortcut text", focus: _p.focus ?? false, icon: _p.icon ?? true, secondaryText: _p.secondaryText ?? false, primaryString: _p.primaryString ?? "Action", secondaryString: _p.secondaryString ?? "Secondary content" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.pxRegular20 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryString}</span>
          {props.secondaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton focus={false} state={"rest"} />}</div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 214,
        height: 32,
        overflow: "hidden",
        borderRadius: "4px 0px 0px 4px",
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={214} height={32} viewBox="0 0 214 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 214,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 214 0 L 215 0 L 215 -1 L 214 -1 L 214 0 Z M 214 32 L 214 33 L 215 33 L 215 32 L 214 32 Z M 4 0 L 4 1 L 214 1 L 214 0 L 214 -1 L 4 -1 L 4 0 Z M 214 0 L 213 0 L 213 32 L 214 32 L 215 32 L 215 0 L 214 0 Z M 214 32 L 214 31 L 4 31 L 4 32 L 4 33 L 214 33 L 214 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.pxFilled20 ?? <Image size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryString}</span>
          {props.secondaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-3-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton focus={false} state={"rest"} />}</div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 214,
        height: 32,
        overflow: "hidden",
        borderRadius: "4px 0px 0px 4px",
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={214} height={32} viewBox="0 0 214 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 214,
          height: 32,
          overflow: "hidden",
          borderRadius: "4px 0px 0px 4px",
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 214 0 L 215 0 L 215 -1 L 214 -1 L 214 0 Z M 214 32 L 214 33 L 215 33 L 215 32 L 214 32 Z M 4 0 L 4 1 L 214 1 L 214 0 L 214 -1 L 4 -1 L 4 0 Z M 214 0 L 213 0 L 213 32 L 214 32 L 215 32 L 215 0 L 214 0 Z M 214 32 L 214 31 L 4 31 L 4 32 L 4 33 L 214 33 L 214 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-hover)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-hover)",
          }}>{props.pxFilled20 ?? <Image size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryString}</span>
          {props.secondaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-3-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton state={"rest"} />}</div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-pressed)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-pressed)",
          }}>{props.pxFilled20 ?? <Image size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryString}</span>
          {props.secondaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-3-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton state={"rest"} />}</div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-selected)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--brand-foreground-compound-rest)",
          }}>{props.pxFilled20 ?? <Image size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.primaryString}</span>
          {props.secondaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-2-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-3-selected)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton state={"rest"} />}</div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxRegular20 ?? <Image size={"20"} theme={"regular"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
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
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton state={"disabled"} />}</div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "6px 2px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        {props.checkmark && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          {props.selected && (
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Checkmark2 size={"16"} theme={"filled"} />}</div>
          )}
        </div>
        )}
        {props.icon && (
        <div style={{
            position: "relative",
            width: 20,
            flexShrink: 0,
            alignSelf: "stretch",
            height: "auto",
            color: "var(--neutral-foreground-disabled-rest)",
          }}>{props.pxFilled20 ?? <Image size={"20"} theme={"filled"} />}</div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          padding: "0px 2px 0px 2px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
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
            fontSize: 10,
            lineHeight: "14px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          )}
        </div>
        {props.endContent && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "right",
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.endContentString}</span>
        </div>
        )}
      </div>
      <div style={{
          position: "relative",
          width: 25,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.icon2 ?? <ItemSplitButton state={"disabled"} />}</div>
    </div>
  );
  const __impls = {
    // figma: Section header=False, State=Rest
    "sectionHeader=false|state=rest": __body0,
    // figma: Section header=False, State=Rest (Checked only)
    "sectionHeader=false|state=rest (checked only)": __body1,
    // figma: Section header=False, State=Hover
    "sectionHeader=false|state=hover": __body2,
    // figma: Section header=False, State=Pressed
    "sectionHeader=false|state=pressed": __body3,
    // figma: Section header=False, State=Selected
    "sectionHeader=false|state=selected": __body4,
    // figma: Section header=False, State=Disabled
    "sectionHeader=false|state=disabled": __body5,
    // figma: Section header=False, State=Disabled (Checked only)
    "sectionHeader=false|state=disabled (checked only)": __body6,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default MenuSplitItem;
