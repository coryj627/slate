import { __slateNorm } from '../internal/slateProps.jsx';
import { Circle2 } from '../internal/Circle2.jsx';
import { Circle3 } from '../internal/Circle3.jsx';
import { Square } from '../internal/Square.jsx';
import { Star } from '../internal/Star.jsx';
import { Star2 } from '../internal/Star2.jsx';

// figma node: 9151:6563 Rating (36 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "size=" + __venc(p.size) + '|' + "compact=" + __venc(p.compact) + '|' + "shape=" + __venc(p.shape);

export function Rating(_p = {}) {
  _p = __slateNorm(_p, {"type":["interactive","display"],"size":["sm","md","lg","xl"],"shape":["stars","circles","square"]});
  const props = { ..._p, ratingQuantity: _p.ratingQuantity ?? "1,160", type: _p.type ?? "interactive", size: _p.size ?? "sm", compact: _p.compact ?? true, shape: _p.shape ?? "stars" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Star2 size={"28"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "3.5"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Circle3 size={"28"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Square size={"28"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Star2 size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Star2 size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Star2 size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Star2 size={"28"} theme={"filled"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -95,
          top: 0,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 44px 0px 44px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--xlarge) * 1px)",
          paddingRight: "calc(var(--xlarge) * 1px)",
        }}>
          <svg width={155} height={28} viewBox="0 0 155 28" fill="none" style={{
            position: "relative",
            width: 155,
            height: 28,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 155 0 L 155 28 L 0 28 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Circle3 size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Circle3 size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Circle3 size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Circle3 size={"28"} theme={"filled"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -95,
          top: 0,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 44px 0px 44px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--xlarge) * 1px)",
          paddingRight: "calc(var(--xlarge) * 1px)",
        }}>
          <svg width={155} height={28} viewBox="0 0 155 28" fill="none" style={{
            position: "relative",
            width: 155,
            height: 28,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 155 0 L 155 28 L 0 28 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Square size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Square size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Square size={"28"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Square size={"28"} theme={"filled"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -95,
          top: 0,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 44px 0px 44px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--xlarge) * 1px)",
          paddingRight: "calc(var(--xlarge) * 1px)",
        }}>
          <svg width={155} height={28} viewBox="0 0 155 28" fill="none" style={{
            position: "relative",
            width: 155,
            height: 28,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 155 0 L 155 28 L 0 28 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 16,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "22px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Star2 size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Star2 size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Star2 size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Star2 size={"28"} theme={"regular"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"28"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -311,
          top: 0,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 152px 0px 152px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--xlarge) * 1px)",
          paddingRight: "calc(var(--xlarge) * 1px)",
        }}>
          <svg width={155} height={28} viewBox="0 0 155 28" fill="none" style={{
            position: "relative",
            width: 155,
            height: 28,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 155 0 L 155 28 L 0 28 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Circle3 size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Circle3 size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Circle3 size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Circle3 size={"28"} theme={"regular"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"28"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Circle2
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -311,
          top: 0,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 152px 0px 152px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--xlarge) * 1px)",
          paddingRight: "calc(var(--xlarge) * 1px)",
        }}>
          <svg width={155} height={28} viewBox="0 0 155 28" fill="none" style={{
            position: "relative",
            width: 155,
            height: 28,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 155 0 L 155 28 L 0 28 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -148,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Square size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Square size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Square size={"28"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Square size={"28"} theme={"regular"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"28"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 28,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"28"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -311,
          top: 0,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 152px 0px 152px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--xlarge) * 1px)",
          paddingRight: "calc(var(--xlarge) * 1px)",
        }}>
          <svg width={155} height={28} viewBox="0 0 155 28" fill="none" style={{
            position: "relative",
            width: 155,
            height: 28,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 155 0 L 155 28 L 0 28 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Star2 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Circle3 size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Square size={"20"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Star2 size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Star2 size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Star2 size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Star2 size={"20"} theme={"filled"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -6,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 33px 0px 33px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--large) * 1px)",
          paddingRight: "calc(var(--large) * 1px)",
        }}>
          <svg width={108} height={20} viewBox="0 0 108 20" fill="none" style={{
            position: "relative",
            width: 108,
            height: 20,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 108 0 L 108 20 L 0 20 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Circle3 size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Circle3 size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Circle3 size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Circle3 size={"20"} theme={"filled"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -6,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 33px 0px 33px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--large) * 1px)",
          paddingRight: "calc(var(--large) * 1px)",
        }}>
          <svg width={108} height={20} viewBox="0 0 108 20" fill="none" style={{
            position: "relative",
            width: 108,
            height: 20,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 108 0 L 108 20 L 0 20 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Square size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Square size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Square size={"20"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Square size={"20"} theme={"filled"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -6,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 33px 0px 33px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--large) * 1px)",
          paddingRight: "calc(var(--large) * 1px)",
        }}>
          <svg width={108} height={20} viewBox="0 0 108 20" fill="none" style={{
            position: "relative",
            width: 108,
            height: 20,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 108 0 L 108 20 L 0 20 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 4px 0px 4px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-vertical-xs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 14,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "20px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Star2 size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Star2 size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Star2 size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Star2 size={"20"} theme={"regular"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -6,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 109px 0px 109px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--large) * 1px)",
          paddingRight: "calc(var(--large) * 1px)",
        }}>
          <svg width={108} height={20} viewBox="0 0 108 20" fill="none" style={{
            position: "relative",
            width: 108,
            height: 20,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 108 0 L 108 20 L 0 20 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Circle3 size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Circle3 size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Circle3 size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Circle3 size={"20"} theme={"regular"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -6,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 109px 0px 109px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--large) * 1px)",
          paddingRight: "calc(var(--large) * 1px)",
        }}>
          <svg width={108} height={20} viewBox="0 0 108 20" fill="none" style={{
            position: "relative",
            width: 108,
            height: 20,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 108 0 L 108 20 L 0 20 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -108,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Square size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Square size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Square size={"20"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Square size={"20"} theme={"regular"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -6,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 109px 0px 109px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--large) * 1px)",
          paddingRight: "calc(var(--large) * 1px)",
        }}>
          <svg width={108} height={20} viewBox="0 0 108 20" fill="none" style={{
            position: "relative",
            width: 108,
            height: 20,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 108 0 L 108 20 L 0 20 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Star2 size={"16"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
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
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Circle3 size={"16"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
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
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Square size={"16"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
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
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Star2 size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Star2 size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Star2 size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Star2 size={"16"} theme={"filled"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -28,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 25px 0px 25px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--medium-2) * 1px)",
          paddingRight: "calc(var(--medium-2) * 1px)",
        }}>
          <svg width={88} height={16} viewBox="0 0 88 16" fill="none" style={{
            position: "relative",
            width: 88,
            height: 16,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 88 0 L 88 16 L 0 16 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Circle3 size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Circle3 size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Circle3 size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Circle3 size={"16"} theme={"filled"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -28,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 25px 0px 25px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--medium-2) * 1px)",
          paddingRight: "calc(var(--medium-2) * 1px)",
        }}>
          <svg width={88} height={16} viewBox="0 0 88 16" fill="none" style={{
            position: "relative",
            width: 88,
            height: 16,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 88 0 L 88 16 L 0 16 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Square size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Square size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Square size={"16"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Square size={"16"} theme={"filled"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -28,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 25px 0px 25px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--medium-2) * 1px)",
          paddingRight: "calc(var(--medium-2) * 1px)",
        }}>
          <svg width={88} height={16} viewBox="0 0 88 16" fill="none" style={{
            position: "relative",
            width: 88,
            height: 16,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 88 0 L 88 16 L 0 16 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Star2 size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Star2 size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Star2 size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Star2 size={"16"} theme={"regular"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Star2
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -28,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 87px 0px 87px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--medium-2) * 1px)",
          paddingRight: "calc(var(--medium-2) * 1px)",
        }}>
          <svg width={88} height={16} viewBox="0 0 88 16" fill="none" style={{
            position: "relative",
            width: 88,
            height: 16,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 88 0 L 88 16 L 0 16 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Circle3 size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Circle3 size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Circle3 size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Circle3 size={"16"} theme={"regular"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -28,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 87px 0px 87px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--medium-2) * 1px)",
          paddingRight: "calc(var(--medium-2) * 1px)",
        }}>
          <svg width={88} height={16} viewBox="0 0 88 16" fill="none" style={{
            position: "relative",
            width: 88,
            height: 16,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 88 0 L 88 16 L 0 16 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -88,
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Square size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Square size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Square size={"16"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Square size={"16"} theme={"regular"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--corner-radius-small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -28,
          top: 0,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 87px 0px 87px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--medium-2) * 1px)",
          paddingRight: "calc(var(--medium-2) * 1px)",
        }}>
          <svg width={88} height={16} viewBox="0 0 88 16" fill="none" style={{
            position: "relative",
            width: 88,
            height: 16,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 88 0 L 88 16 L 0 16 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Star2 size={"12"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
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
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Circle3 size={"12"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
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
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--foreground-2)",
            }}>{props.icon1 ?? <Square size={"12"} theme={"filled"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
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
      }}>{props.text2 ?? "·"}</span>
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
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Star2 size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Star2 size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Star2 size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Star2 size={"12"} theme={"filled"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -48,
          top: 2,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 19px 0px 19px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--small-2) * 1px)",
          paddingRight: "calc(var(--small-2) * 1px)",
        }}>
          <svg width={68} height={12} viewBox="0 0 68 12" fill="none" style={{
            position: "relative",
            width: 68,
            height: 12,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 68 0 L 68 12 L 0 12 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Circle3 size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Circle3 size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Circle3 size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Circle3 size={"12"} theme={"filled"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -48,
          top: 2,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 19px 0px 19px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--small-2) * 1px)",
          paddingRight: "calc(var(--small-2) * 1px)",
        }}>
          <svg width={68} height={12} viewBox="0 0 68 12" fill="none" style={{
            position: "relative",
            width: 68,
            height: 12,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 68 0 L 68 12 L 0 12 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon1 ?? <Square size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon2 ?? <Square size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon3 ?? <Square size={"12"} theme={"filled"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}>{props.icon4 ?? <Square size={"12"} theme={"filled"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-display)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -48,
          top: 2,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 19px 0px 19px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--small-2) * 1px)",
          paddingRight: "calc(var(--small-2) * 1px)",
        }}>
          <svg width={68} height={12} viewBox="0 0 68 12" fill="none" style={{
            position: "relative",
            width: 68,
            height: 12,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 68 0 L 68 12 L 0 12 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        padding: "0px 2px 0px 2px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "right",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.text1 ?? "0"}</span>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text2 ?? "·"}</span>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        textAlign: "right",
        whiteSpace: "nowrap",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.ratingQuantity}</span>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Star2 size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Star2 size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Star2 size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Star2 size={"12"} theme={"regular"} />}</div>
          <Star2
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"12"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Star
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-background-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -48,
          top: 2,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 73px 0px 73px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--small-2) * 1px)",
          paddingRight: "calc(var(--small-2) * 1px)",
        }}>
          <svg width={68} height={12} viewBox="0 0 68 12" fill="none" style={{
            position: "relative",
            width: 68,
            height: 12,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 68 0 L 68 12 L 0 12 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Circle3 size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Circle3 size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Circle3 size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Circle3 size={"12"} theme={"regular"} />}</div>
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"12"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Circle3
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -48,
          top: 2,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 73px 0px 73px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--small-2) * 1px)",
          paddingRight: "calc(var(--small-2) * 1px)",
        }}>
          <svg width={68} height={12} viewBox="0 0 68 12" fill="none" style={{
            position: "relative",
            width: 68,
            height: 12,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 68 0 L 68 12 L 0 12 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: -68,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon1 ?? <Square size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon2 ?? <Square size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon3 ?? <Square size={"12"} theme={"regular"} />}</div>
          <div style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}>{props.icon4 ?? <Square size={"12"} theme={"regular"} />}</div>
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--background-3)",
            }}
            size={"12"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--small) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
          <Square
            style={{
              position: "relative",
              width: 12,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-1-rest)",
            }}
            size={"12"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: -48,
          top: 2,
          width: 115,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          padding: "0px 73px 0px 73px",
          alignItems: "flex-end",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--small-2) * 1px)",
          paddingRight: "calc(var(--small-2) * 1px)",
        }}>
          <svg width={68} height={12} viewBox="0 0 68 12" fill="none" style={{
            position: "relative",
            width: 68,
            height: 12,
            flexShrink: 0,
            color: "var(--foreground-2)",
          }}>
            <path d={"M 0 0 L 68 0 L 68 12 L 0 12 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Type=Display, Size=XLarge, Compact=True, Shape=Stars
    "type=display|size=xl|compact=true|shape=stars": __body0,
    // figma: Type=Display, Size=XLarge, Compact=True, Shape=Circles
    "type=display|size=xl|compact=true|shape=circles": __body1,
    // figma: Type=Display, Size=XLarge, Compact=True, Shape=Square
    "type=display|size=xl|compact=true|shape=square": __body2,
    // figma: Type=Display, Size=XLarge, Compact=False, Shape=Stars
    "type=display|size=xl|compact=false|shape=stars": __body3,
    // figma: Type=Display, Size=XLarge, Compact=False, Shape=Circles
    "type=display|size=xl|compact=false|shape=circles": __body4,
    // figma: Type=Display, Size=XLarge, Compact=False, Shape=Square
    "type=display|size=xl|compact=false|shape=square": __body5,
    // figma: Type=Interactive, Size=XLarge, Compact=False, Shape=Stars
    "type=interactive|size=xl|compact=false|shape=stars": __body6,
    // figma: Type=Interactive, Size=XLarge, Compact=False, Shape=Circles
    "type=interactive|size=xl|compact=false|shape=circles": __body7,
    // figma: Type=Interactive, Size=XLarge, Compact=False, Shape=Square
    "type=interactive|size=xl|compact=false|shape=square": __body8,
    // figma: Type=Display, Size=Large, Compact=True, Shape=Stars
    "type=display|size=lg|compact=true|shape=stars": __body9,
    // figma: Type=Display, Size=Large, Compact=True, Shape=Circles
    "type=display|size=lg|compact=true|shape=circles": __body10,
    // figma: Type=Display, Size=Large, Compact=True, Shape=Square
    "type=display|size=lg|compact=true|shape=square": __body11,
    // figma: Type=Display, Size=Large, Compact=False, Shape=Stars
    "type=display|size=lg|compact=false|shape=stars": __body12,
    // figma: Type=Display, Size=Large, Compact=False, Shape=Circles
    "type=display|size=lg|compact=false|shape=circles": __body13,
    // figma: Type=Display, Size=Large, Compact=False, Shape=Square
    "type=display|size=lg|compact=false|shape=square": __body14,
    // figma: Type=Interactive, Size=Large, Compact=False, Shape=Stars
    "type=interactive|size=lg|compact=false|shape=stars": __body15,
    // figma: Type=Interactive, Size=Large, Compact=False, Shape=Circles
    "type=interactive|size=lg|compact=false|shape=circles": __body16,
    // figma: Type=Interactive, Size=Large, Compact=False, Shape=Square
    "type=interactive|size=lg|compact=false|shape=square": __body17,
    // figma: Type=Display, Size=Medium, Compact=True, Shape=Stars
    "type=display|size=md|compact=true|shape=stars": __body18,
    // figma: Type=Display, Size=Medium, Compact=True, Shape=Circles
    "type=display|size=md|compact=true|shape=circles": __body19,
    // figma: Type=Display, Size=Medium, Compact=True, Shape=Square
    "type=display|size=md|compact=true|shape=square": __body20,
    // figma: Type=Display, Size=Medium, Compact=False, Shape=Stars
    "type=display|size=md|compact=false|shape=stars": __body21,
    // figma: Type=Display, Size=Medium, Compact=False, Shape=Circles
    "type=display|size=md|compact=false|shape=circles": __body22,
    // figma: Type=Display, Size=Medium, Compact=False, Shape=Square
    "type=display|size=md|compact=false|shape=square": __body23,
    // figma: Type=Interactive, Size=Medium, Compact=False, Shape=Stars
    "type=interactive|size=md|compact=false|shape=stars": __body24,
    // figma: Type=Interactive, Size=Medium, Compact=False, Shape=Circles
    "type=interactive|size=md|compact=false|shape=circles": __body25,
    // figma: Type=Interactive, Size=Medium, Compact=False, Shape=Square
    "type=interactive|size=md|compact=false|shape=square": __body26,
    // figma: Type=Display, Size=Small, Compact=True, Shape=Stars
    "type=display|size=sm|compact=true|shape=stars": __body27,
    // figma: Type=Display, Size=Small, Compact=True, Shape=Circles
    "type=display|size=sm|compact=true|shape=circles": __body28,
    // figma: Type=Display, Size=Small, Compact=True, Shape=Square
    "type=display|size=sm|compact=true|shape=square": __body29,
    // figma: Type=Display, Size=Small, Compact=False, Shape=Stars
    "type=display|size=sm|compact=false|shape=stars": __body30,
    // figma: Type=Display, Size=Small, Compact=False, Shape=Circles
    "type=display|size=sm|compact=false|shape=circles": __body31,
    // figma: Type=Display, Size=Small, Compact=False, Shape=Square
    "type=display|size=sm|compact=false|shape=square": __body32,
    // figma: Type=Interactive, Size=Small, Compact=False, Shape=Stars
    "type=interactive|size=sm|compact=false|shape=stars": __body33,
    // figma: Type=Interactive, Size=Small, Compact=False, Shape=Circles
    "type=interactive|size=sm|compact=false|shape=circles": __body34,
    // figma: Type=Interactive, Size=Small, Compact=False, Shape=Square
    "type=interactive|size=sm|compact=false|shape=square": __body35,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Rating;
