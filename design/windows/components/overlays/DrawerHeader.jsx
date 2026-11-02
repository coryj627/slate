import { Divider } from '../content/Divider.jsx';

// figma node: 9170:8098 .Drawer header (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "navigation=" + __venc(p.navigation);

export function DrawerHeader(_p = {}) {
  const props = { ..._p, navigation: _p.navigation ?? false, leftActions: _p.leftActions ?? true, title: _p.title ?? "Title", dismiss: _p.dismiss ?? true, divider: _p.divider ?? false, rightActions: _p.rightActions ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 592,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        padding: "24px 16px 12px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              lineHeight: "26px",
              color: "var(--neutral-foreground-1-rest)",
              flexGrow: 1,
            }}>{props.title}</span>
          </div>
          {props.dismiss && (
          <div style={{
            position: "relative",
            borderRadius: 4,
            backgroundColor: "rgba(255,255,255,0)",
            display: "flex",
            flexDirection: "row",
            padding: "6px 6px 6px 6px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                width: 20,
                height: 20,
                overflow: "hidden",
                flexShrink: 0,
              }}>
                <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
                  position: "absolute",
                  left: 4,
                  top: 4,
                  width: 12,
                  height: 12,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
          )}
        </div>
      </div>
      {props.divider && (
      <Divider
        style={{
          position: "relative",
          height: 1,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        contentSlot={false}
        layout={"center (default)"}
        vertical={false}
      />
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 592,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "16px 16px 10px 16px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.leftActions && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              borderRadius: 4,
              backgroundColor: "rgba(255,255,255,0)",
              display: "flex",
              flexDirection: "row",
              padding: "6px 6px 6px 6px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
                justifyContent: "center",
                alignItems: "center",
                flexWrap: "nowrap",
                flexShrink: 0,
              }}>
                <div style={{
                  position: "relative",
                  width: 20,
                  height: 20,
                  overflow: "hidden",
                  flexShrink: 0,
                }}>
                  <svg width={16} height={13.997} viewBox="0 0 16 13.997" fill="none" style={{
                    position: "absolute",
                    left: 1.996,
                    top: 3,
                    width: 16,
                    height: 13.997,
                    color: "var(--neutral-foreground-2-rest)",
                  }}>
                    <path d={"M 7.163 13.867 C 7.367 14.053 7.683 14.038 7.869 13.834 C 8.055 13.631 8.041 13.314 7.837 13.128 L 1.669 7.497 L 15.5 7.497 C 15.776 7.497 16 7.274 16 6.997 C 16 6.721 15.776 6.497 15.5 6.497 L 1.672 6.497 L 7.837 0.869 C 8.041 0.683 8.055 0.367 7.869 0.163 C 7.683 -0.041 7.367 -0.055 7.163 0.131 L 0.247 6.445 C 0.107 6.573 0.027 6.74 0.007 6.913 C 0.002 6.941 0 6.969 0 6.997 C 0 7.024 0.002 7.051 0.006 7.076 C 0.024 7.252 0.104 7.423 0.247 7.553 L 7.163 13.867 Z"} fill="currentColor" fillRule="nonzero" />
                  </svg>
                </div>
              </div>
            </div>
          </div>
          )}
          {props.rightActions && (
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            justifyContent: "flex-end",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              borderRadius: 4,
              backgroundColor: "rgba(255,255,255,0)",
              display: "flex",
              flexDirection: "row",
              padding: "6px 6px 6px 6px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
                justifyContent: "center",
                alignItems: "center",
                flexWrap: "nowrap",
                flexShrink: 0,
              }}>
                <div style={{
                  position: "relative",
                  width: 20,
                  height: 20,
                  overflow: "hidden",
                  flexShrink: 0,
                }}>
                  <svg width={13.997} height={16} viewBox="0 0 13.997 16" fill="none" style={{
                    position: "absolute",
                    left: 3.001,
                    top: 1.999,
                    width: 13.997,
                    height: 16,
                    color: "var(--neutral-foreground-2-rest)",
                  }}>
                    <path d={"M 0.131 7.163 C -0.055 7.367 -0.041 7.683 0.163 7.869 C 0.367 8.055 0.683 8.041 0.869 7.837 L 6.5 1.669 L 6.5 15.5 C 6.5 15.776 6.724 16 7 16 C 7.276 16 7.5 15.776 7.5 15.5 L 7.5 1.672 L 13.128 7.837 C 13.314 8.041 13.631 8.055 13.834 7.869 C 14.038 7.683 14.053 7.367 13.867 7.163 L 7.553 0.247 C 7.425 0.107 7.257 0.027 7.084 0.007 C 7.057 0.002 7.029 0 7 0 C 6.973 0 6.947 0.002 6.921 0.006 C 6.745 0.024 6.574 0.104 6.445 0.247 L 0.131 7.163 Z"} fill="currentColor" fillRule="nonzero" />
                  </svg>
                </div>
              </div>
            </div>
            <div style={{
              position: "relative",
              borderRadius: 4,
              backgroundColor: "rgba(255,255,255,0)",
              display: "flex",
              flexDirection: "row",
              padding: "6px 6px 6px 6px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
                justifyContent: "center",
                alignItems: "center",
                flexWrap: "nowrap",
                flexShrink: 0,
              }}>
                <div style={{
                  position: "relative",
                  width: 20,
                  height: 20,
                  overflow: "hidden",
                  flexShrink: 0,
                }}>
                  <svg width={13.997} height={16} viewBox="0 0 13.997 16" fill="none" style={{
                    position: "absolute",
                    left: 3,
                    top: 2,
                    width: 13.997,
                    height: 16,
                    color: "var(--neutral-foreground-2-rest)",
                  }}>
                    <path d={"M 13.867 8.837 C 14.053 8.633 14.038 8.317 13.834 8.131 C 13.631 7.945 13.314 7.959 13.128 8.163 L 7.497 14.331 L 7.497 0.5 C 7.497 0.224 7.274 0 6.997 0 C 6.721 0 6.497 0.224 6.497 0.5 L 6.497 14.328 L 0.869 8.163 C 0.683 7.959 0.367 7.945 0.163 8.131 C -0.041 8.317 -0.055 8.633 0.131 8.837 L 6.445 15.753 C 6.573 15.893 6.74 15.973 6.913 15.993 C 6.941 15.998 6.969 16 6.997 16 C 7.024 16 7.051 15.998 7.076 15.994 C 7.252 15.976 7.423 15.896 7.553 15.753 L 13.867 8.837 Z"} fill="currentColor" fillRule="nonzero" />
                  </svg>
                </div>
              </div>
            </div>
            <div style={{
              position: "relative",
              borderRadius: 4,
              backgroundColor: "rgba(255,255,255,0)",
              display: "flex",
              flexDirection: "row",
              padding: "6px 6px 6px 6px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
                justifyContent: "center",
                alignItems: "center",
                flexWrap: "nowrap",
                flexShrink: 0,
              }}>
                <div style={{
                  position: "relative",
                  width: 20,
                  height: 20,
                  overflow: "hidden",
                  flexShrink: 0,
                }}>
                  <svg width={16.227} height={17} viewBox="0 0 16.227 17" fill="none" style={{
                    position: "absolute",
                    left: 1.886,
                    top: 1.5,
                    width: 16.227,
                    height: 17,
                    color: "var(--neutral-foreground-2-rest)",
                  }}>
                    <path d={"M 0.024 5.883 C 0.394 4.741 1.002 3.692 1.805 2.804 C 1.94 2.653 2.153 2.6 2.344 2.668 L 4.262 3.354 C 4.782 3.54 5.355 3.269 5.54 2.749 C 5.559 2.698 5.573 2.645 5.583 2.592 L 5.948 0.586 C 5.984 0.386 6.138 0.229 6.336 0.186 C 6.916 0.063 7.511 0 8.114 0 C 8.716 0 9.31 0.063 9.89 0.186 C 10.088 0.228 10.242 0.386 10.278 0.585 L 10.645 2.592 C 10.744 3.135 11.264 3.495 11.808 3.396 C 11.861 3.386 11.914 3.372 11.965 3.354 L 13.883 2.668 C 14.074 2.6 14.287 2.653 14.422 2.804 C 15.225 3.692 15.834 4.741 16.203 5.883 C 16.265 6.075 16.205 6.287 16.051 6.418 L 14.496 7.738 C 14.075 8.095 14.023 8.726 14.381 9.147 C 14.416 9.189 14.454 9.227 14.496 9.262 L 16.051 10.582 C 16.205 10.713 16.265 10.925 16.203 11.117 C 15.834 12.259 15.225 13.308 14.422 14.196 C 14.287 14.347 14.074 14.4 13.883 14.332 L 11.965 13.646 C 11.445 13.46 10.872 13.731 10.687 14.251 C 10.668 14.302 10.654 14.355 10.645 14.408 L 10.278 16.415 C 10.242 16.614 10.088 16.772 9.89 16.814 C 9.31 16.937 8.716 17 8.114 17 C 7.511 17 6.916 16.937 6.336 16.814 C 6.138 16.771 5.984 16.614 5.948 16.414 L 5.583 14.408 C 5.483 13.865 4.963 13.505 4.419 13.604 C 4.366 13.614 4.313 13.628 4.262 13.646 L 2.344 14.332 C 2.153 14.4 1.94 14.347 1.805 14.196 C 1.002 13.308 0.394 12.259 0.024 11.117 C -0.038 10.925 0.022 10.713 0.176 10.582 L 1.731 9.262 C 2.152 8.905 2.204 8.274 1.846 7.853 C 1.811 7.811 1.773 7.773 1.731 7.738 L 0.176 6.418 C 0.022 6.287 -0.038 6.075 0.024 5.883 Z M 1.085 5.877 L 2.379 6.975 C 2.461 7.046 2.538 7.123 2.609 7.205 C 3.323 8.047 3.22 9.31 2.378 10.025 L 1.085 11.123 C 1.377 11.905 1.798 12.632 2.33 13.275 L 3.926 12.704 C 4.028 12.668 4.133 12.64 4.24 12.62 C 5.327 12.422 6.368 13.142 6.566 14.229 L 6.87 15.898 C 7.278 15.966 7.694 16 8.114 16 C 8.533 16 8.948 15.966 9.356 15.898 L 9.661 14.229 C 9.68 14.122 9.708 14.017 9.745 13.915 C 10.117 12.875 11.261 12.333 12.301 12.704 L 13.897 13.275 C 14.429 12.632 14.85 11.905 15.142 11.123 L 13.849 10.025 C 13.766 9.954 13.689 9.877 13.619 9.795 C 12.904 8.953 13.007 7.69 13.849 6.975 L 15.142 5.877 C 14.85 5.095 14.429 4.368 13.897 3.725 L 12.301 4.296 C 12.199 4.332 12.094 4.36 11.987 4.38 C 10.9 4.578 9.859 3.858 9.661 2.771 L 9.356 1.102 C 8.948 1.034 8.533 1 8.114 1 C 7.694 1 7.278 1.034 6.87 1.102 L 6.566 2.771 C 6.547 2.878 6.519 2.983 6.482 3.085 C 6.11 4.125 4.966 4.667 3.926 4.296 L 2.33 3.725 C 1.798 4.368 1.377 5.095 1.085 5.877 Z M 5.614 8.5 C 5.614 7.119 6.733 6 8.114 6 C 9.494 6 10.614 7.119 10.614 8.5 C 10.614 9.881 9.494 11 8.114 11 C 6.733 11 5.614 9.881 5.614 8.5 Z M 6.614 8.5 C 6.614 9.328 7.285 10 8.114 10 C 8.942 10 9.614 9.328 9.614 8.5 C 9.614 7.672 8.942 7 8.114 7 C 7.285 7 6.614 7.672 6.614 8.5 Z"} fill="currentColor" fillRule="nonzero" />
                  </svg>
                </div>
              </div>
            </div>
            <div style={{
              position: "relative",
              borderRadius: 4,
              backgroundColor: "rgba(255,255,255,0)",
              display: "flex",
              flexDirection: "row",
              padding: "6px 6px 6px 6px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
                justifyContent: "center",
                alignItems: "center",
                flexWrap: "nowrap",
                flexShrink: 0,
              }}>
                <div style={{
                  position: "relative",
                  width: 20,
                  height: 20,
                  overflow: "hidden",
                  flexShrink: 0,
                }}>
                  <svg width={14} height={14} viewBox="0 0 14 14" fill="none" style={{
                    position: "absolute",
                    left: 3,
                    top: 3,
                    width: 14,
                    height: 14,
                    color: "var(--neutral-foreground-2-rest)",
                  }}>
                    <path d={"M 1 7 C 1 3.686 3.686 1 7 1 C 8.777 1 10.373 1.772 11.472 3 L 9.5 3 C 9.224 3 9 3.224 9 3.5 C 9 3.776 9.224 4 9.5 4 L 12.5 4 C 12.776 4 13 3.776 13 3.5 L 13 0.5 C 13 0.224 12.776 0 12.5 0 C 12.224 0 12 0.224 12 0.5 L 12 2.101 C 10.73 0.805 8.959 0 7 0 C 3.134 0 0 3.134 0 7 C 0 10.866 3.134 14 7 14 C 10.866 14 14 10.866 14 7 C 14 6.819 13.993 6.64 13.98 6.462 C 13.959 6.187 13.718 5.98 13.443 6.001 C 13.168 6.022 12.962 6.263 12.982 6.538 C 12.994 6.69 13 6.844 13 7 C 13 10.314 10.314 13 7 13 C 3.686 13 1 10.314 1 7 Z"} fill="currentColor" fillRule="nonzero" />
                  </svg>
                </div>
              </div>
            </div>
            <div style={{
              position: "relative",
              borderRadius: 4,
              backgroundColor: "rgba(255,255,255,0)",
              display: "flex",
              flexDirection: "row",
              padding: "6px 6px 6px 6px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
                justifyContent: "center",
                alignItems: "center",
                flexWrap: "nowrap",
                flexShrink: 0,
              }}>
                <div style={{
                  position: "relative",
                  width: 20,
                  height: 20,
                  overflow: "hidden",
                  flexShrink: 0,
                }}>
                  <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
                    position: "absolute",
                    left: 4,
                    top: 4,
                    width: 12,
                    height: 12,
                    color: "var(--neutral-foreground-2-rest)",
                  }}>
                    <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                  </svg>
                </div>
              </div>
            </div>
          </div>
          )}
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 24px 12px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          lineHeight: "26px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.title}</span>
      </div>
      {props.divider && (
      <Divider
        style={{
          position: "relative",
          height: 1,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}
        contentSlot={false}
        layout={"center (default)"}
        vertical={false}
      />
      )}
    </div>
  );
  const __impls = {
    // figma: Navigation=False
    "navigation=false": __body0,
    // figma: Navigation=True
    "navigation=true": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default DrawerHeader;
