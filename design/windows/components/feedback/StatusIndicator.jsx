import { __slateNorm } from '../internal/slateProps.jsx';
import { ArrowCircleDownRight } from '../internal/ArrowCircleDownRight.jsx';
import { ArrowClockwiseDashes } from '../internal/ArrowClockwiseDashes.jsx';
import { ArrowSync } from '../internal/ArrowSync.jsx';
import { CheckmarkCircle } from '../internal/CheckmarkCircle.jsx';
import { Circle } from '../internal/Circle.jsx';
import { CircleHalfFill } from '../internal/CircleHalfFill.jsx';
import { Clock } from '../internal/Clock.jsx';
import { DiamondDismiss } from '../internal/DiamondDismiss.jsx';
import { Info } from '../internal/Info.jsx';
import { PauseCircle } from '../internal/PauseCircle.jsx';
import { QuestionCircle } from '../internal/QuestionCircle.jsx';
import { SubtractCircle } from '../internal/SubtractCircle.jsx';
import { Warning } from '../internal/Warning.jsx';

// figma node: 9116:27465 .StatusIndicator (48 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "message=" + __venc(p.message) + '|' + "size=" + __venc(p.size) + '|' + "category=" + __venc(p.category);

export function StatusIndicator(_p = {}) {
  _p = __slateNorm(_p, {"message":["archived","cancelled","draft","failed","inprogress","new","not started","pause","pending","scheduled","success","synced","syncing","unknown","warning","generic information"],"size":["medium (default)","sm","lg"],"category":["informational/neutral","success","error","active/ in progress","warning"]});
  const props = { ..._p, message: _p.message ?? "archived", size: _p.size ?? "medium (default)", category: _p.category ?? "informational/neutral", label: _p.label ?? true, genericLabel: _p.genericLabel ?? "Information" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest)",
          }}>{props.icon1 ?? <CheckmarkCircle size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Success"}</span>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest)",
          }}>{props.icon1 ?? <CheckmarkCircle size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Success"}</span>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest)",
          }}>{props.icon1 ?? <CheckmarkCircle size={"12"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Success"}</span>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest)",
          }}>{props.icon1 ?? <ArrowSync size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Synced"}</span>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest)",
          }}>{props.icon1 ?? <ArrowSync size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Synced"}</span>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest)",
          }}>{props.icon1 ?? <ArrowSync size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Synced"}</span>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest)",
          }}>{props.icon1 ?? <DiamondDismiss size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Failed"}</span>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest)",
          }}>{props.icon1 ?? <DiamondDismiss size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Failed"}</span>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest)",
          }}>{props.icon1 ?? <DiamondDismiss size={"12"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Failed"}</span>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest)",
          }}>{props.icon1 ?? <SubtractCircle size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Cancelled"}</span>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest)",
          }}>{props.icon1 ?? <SubtractCircle size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Cancelled"}</span>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest)",
          }}>{props.icon1 ?? <SubtractCircle size={"12"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Cancelled"}</span>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-1-rest)",
          }}>{props.icon1 ?? <Warning size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Warning"}</span>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-warning-foreground-1-rest)",
          }}>{props.icon1 ?? <Warning size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Warning"}</span>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-1-rest)",
          }}>{props.icon1 ?? <Warning size={"12"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Warning"}</span>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <Circle size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "New"}</span>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <Circle size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "New"}</span>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <Circle size={"12"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "New"}</span>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <ArrowSync size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Syncing"}</span>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <ArrowSync size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Syncing"}</span>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <ArrowSync size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Syncing"}</span>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <ArrowCircleDownRight size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Archived"}</span>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <ArrowCircleDownRight size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Archived"}</span>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <ArrowCircleDownRight size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Archived"}</span>
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Clock size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Scheduled"}</span>
      )}
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.iconX16 ?? <Info size={"16"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.genericLabel}</span>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Clock size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Scheduled"}</span>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.iconX20 ?? <Info size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.genericLabel}</span>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Clock size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Scheduled"}</span>
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.iconX12 ?? <Info size={"12"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.genericLabel}</span>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <QuestionCircle size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Unknown"}</span>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <QuestionCircle size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Unknown"}</span>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <QuestionCircle size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Unknown"}</span>
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <ArrowClockwiseDashes size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Pending"}</span>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <ArrowClockwiseDashes size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Pending"}</span>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <ArrowClockwiseDashes size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Pending"}</span>
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Circle size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Not started"}</span>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Circle size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Not started"}</span>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Circle size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Not started"}</span>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <PauseCircle size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Pause"}</span>
      )}
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <PauseCircle size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Pause"}</span>
      )}
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <PauseCircle size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Pause"}</span>
      )}
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <CircleHalfFill size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Draft"}</span>
      )}
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <CircleHalfFill size={"20"} theme={"filled"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Draft"}</span>
      )}
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <CircleHalfFill size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Draft"}</span>
      )}
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <CircleHalfFill size={"16"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "In progress"}</span>
      )}
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <CircleHalfFill size={"20"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "In progress"}</span>
      )}
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xs-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--vertical-none-2) * 1px)",
      paddingRight: "calc(var(--horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest)",
          }}>{props.icon1 ?? <CircleHalfFill size={"12"} theme={"regular"} />}</div>
      </div>
      {props.label && (
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "In progress"}</span>
      )}
    </div>
  );
  const __impls = {
    // figma: Message=Success, Size=Medium (default), Category=Success
    "message=success|size=medium (default)|category=success": __body0,
    // figma: Message=Success, Size=Large, Category=Success
    "message=success|size=lg|category=success": __body1,
    // figma: Message=Success, Size=Small, Category=Success
    "message=success|size=sm|category=success": __body2,
    // figma: Message=Synced, Size=Medium (default), Category=Success
    "message=synced|size=medium (default)|category=success": __body3,
    // figma: Message=Synced, Size=Large, Category=Success
    "message=synced|size=lg|category=success": __body4,
    // figma: Message=Synced, Size=Small, Category=Success
    "message=synced|size=sm|category=success": __body5,
    // figma: Message=Failed, Size=Medium (default), Category=Error
    "message=failed|size=medium (default)|category=error": __body6,
    // figma: Message=Failed, Size=Large, Category=Error
    "message=failed|size=lg|category=error": __body7,
    // figma: Message=Failed, Size=Small, Category=Error
    "message=failed|size=sm|category=error": __body8,
    // figma: Message=Cancelled, Size=Medium (default), Category=Error
    "message=cancelled|size=medium (default)|category=error": __body9,
    // figma: Message=Cancelled, Size=Large, Category=Error
    "message=cancelled|size=lg|category=error": __body10,
    // figma: Message=Cancelled, Size=Small, Category=Error
    "message=cancelled|size=sm|category=error": __body11,
    // figma: Message=Warning, Size=Medium (default), Category=Warning
    "message=warning|size=medium (default)|category=warning": __body12,
    // figma: Message=Warning, Size=Large, Category=Warning
    "message=warning|size=lg|category=warning": __body13,
    // figma: Message=Warning, Size=Small, Category=Warning
    "message=warning|size=sm|category=warning": __body14,
    // figma: Message=New, Size=Medium (default), Category=Active/ In Progress
    "message=new|size=medium (default)|category=active/ in progress": __body15,
    // figma: Message=New, Size=Large, Category=Active/ In Progress
    "message=new|size=lg|category=active/ in progress": __body16,
    // figma: Message=New, Size=Small, Category=Active/ In Progress
    "message=new|size=sm|category=active/ in progress": __body17,
    // figma: Message=Syncing, Size=Medium (default), Category=Active/ In Progress
    "message=syncing|size=medium (default)|category=active/ in progress": __body18,
    // figma: Message=Syncing, Size=Large, Category=Active/ In Progress
    "message=syncing|size=lg|category=active/ in progress": __body19,
    // figma: Message=Syncing, Size=Small, Category=Active/ In Progress
    "message=syncing|size=sm|category=active/ in progress": __body20,
    // figma: Message=Archived, Size=Medium (default), Category=Informational/Neutral
    "message=archived|size=medium (default)|category=informational/neutral": __body21,
    // figma: Message=Archived, Size=Large, Category=Informational/Neutral
    "message=archived|size=lg|category=informational/neutral": __body22,
    // figma: Message=Archived, Size=Small, Category=Informational/Neutral
    "message=archived|size=sm|category=informational/neutral": __body23,
    // figma: Message=Scheduled, Size=Medium (default), Category=Informational/Neutral
    "message=scheduled|size=medium (default)|category=informational/neutral": __body24,
    // figma: Message=Generic information, Size=Medium (default), Category=Informational/Neutral
    "message=generic information|size=medium (default)|category=informational/neutral": __body25,
    // figma: Message=Scheduled, Size=Large, Category=Informational/Neutral
    "message=scheduled|size=lg|category=informational/neutral": __body26,
    // figma: Message=Generic information, Size=Large, Category=Informational/Neutral
    "message=generic information|size=lg|category=informational/neutral": __body27,
    // figma: Message=Scheduled, Size=Small, Category=Informational/Neutral
    "message=scheduled|size=sm|category=informational/neutral": __body28,
    // figma: Message=Generic information, Size=Small, Category=Informational/Neutral
    "message=generic information|size=sm|category=informational/neutral": __body29,
    // figma: Message=Unknown, Size=Medium (default), Category=Informational/Neutral
    "message=unknown|size=medium (default)|category=informational/neutral": __body30,
    // figma: Message=Unknown, Size=Large, Category=Informational/Neutral
    "message=unknown|size=lg|category=informational/neutral": __body31,
    // figma: Message=Unknown, Size=Small, Category=Informational/Neutral
    "message=unknown|size=sm|category=informational/neutral": __body32,
    // figma: Message=Pending, Size=Medium (default), Category=Informational/Neutral
    "message=pending|size=medium (default)|category=informational/neutral": __body33,
    // figma: Message=Pending, Size=Large, Category=Informational/Neutral
    "message=pending|size=lg|category=informational/neutral": __body34,
    // figma: Message=Pending, Size=Small, Category=Informational/Neutral
    "message=pending|size=sm|category=informational/neutral": __body35,
    // figma: Message=Not started, Size=Medium (default), Category=Informational/Neutral
    "message=not started|size=medium (default)|category=informational/neutral": __body36,
    // figma: Message=Not started, Size=Large, Category=Informational/Neutral
    "message=not started|size=lg|category=informational/neutral": __body37,
    // figma: Message=Not started, Size=Small, Category=Informational/Neutral
    "message=not started|size=sm|category=informational/neutral": __body38,
    // figma: Message=Pause, Size=Medium (default), Category=Informational/Neutral
    "message=pause|size=medium (default)|category=informational/neutral": __body39,
    // figma: Message=Pause, Size=Large, Category=Informational/Neutral
    "message=pause|size=lg|category=informational/neutral": __body40,
    // figma: Message=Pause, Size=Small, Category=Informational/Neutral
    "message=pause|size=sm|category=informational/neutral": __body41,
    // figma: Message=Draft, Size=Medium (default), Category=Informational/Neutral
    "message=draft|size=medium (default)|category=informational/neutral": __body42,
    // figma: Message=Draft, Size=Large, Category=Informational/Neutral
    "message=draft|size=lg|category=informational/neutral": __body43,
    // figma: Message=Draft, Size=Small, Category=Informational/Neutral
    "message=draft|size=sm|category=informational/neutral": __body44,
    // figma: Message=InProgress, Size=Medium (default), Category=Active/ In Progress
    "message=inprogress|size=medium (default)|category=active/ in progress": __body45,
    // figma: Message=InProgress, Size=Large, Category=Active/ In Progress
    "message=inprogress|size=lg|category=active/ in progress": __body46,
    // figma: Message=InProgress, Size=Small, Category=Active/ In Progress
    "message=inprogress|size=sm|category=active/ in progress": __body47,
  };
  return (__impls[__vkey(props)] ?? __body21)();
}
export default StatusIndicator;
