import { Chevron } from './Chevron.jsx';

// figma node: 9121:6365 .Custom menu example
export function CustomMenuExample(_p = {}) {
  const props = _p;
  return (
    <div className={props.className} style={{
      width: 260,
      borderRadius: 24,
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-xxs) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={12} height={16} viewBox="0 0 12 16" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 2,
              width: 12,
              height: 16,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 8.854 7.854 C 9.049 7.658 9.049 7.342 8.854 7.146 C 8.658 6.951 8.342 6.951 8.146 7.146 L 5 10.293 L 3.854 9.146 C 3.658 8.951 3.342 8.951 3.146 9.146 C 2.951 9.342 2.951 9.658 3.146 9.854 L 4.646 11.354 C 4.842 11.549 5.158 11.549 5.354 11.354 L 8.854 7.854 Z M 4.5 0 C 3.847 0 3.291 0.417 3.085 1 L 1.5 1 C 0.672 1 0 1.672 0 2.5 L 0 14.5 C 0 15.328 0.672 16 1.5 16 L 10.5 16 C 11.328 16 12 15.328 12 14.5 L 12 2.5 C 12 1.672 11.328 1 10.5 1 L 8.915 1 C 8.709 0.417 8.153 0 7.5 0 L 4.5 0 Z M 4 1.5 C 4 1.224 4.224 1 4.5 1 L 7.5 1 C 7.776 1 8 1.224 8 1.5 C 8 1.776 7.776 2 7.5 2 L 4.5 2 C 4.224 2 4 1.776 4 1.5 Z M 1.5 2 L 3.085 2 C 3.291 2.583 3.847 3 4.5 3 L 7.5 3 C 8.153 3 8.709 2.583 8.915 2 L 10.5 2 C 10.776 2 11 2.224 11 2.5 L 11 14.5 C 11 14.776 10.776 15 10.5 15 L 1.5 15 C 1.224 15 1 14.776 1 14.5 L 1 2.5 C 1 2.224 1.224 2 1.5 2 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>New linked work item</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={12} height={14} viewBox="0 0 12 14" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 3,
              width: 12,
              height: 14,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 8.146 0.146 C 8.342 -0.049 8.658 -0.049 8.854 0.146 L 11.854 3.146 C 12.049 3.342 12.049 3.658 11.854 3.854 L 8.854 6.854 C 8.658 7.049 8.342 7.049 8.146 6.854 C 7.951 6.658 7.951 6.342 8.146 6.146 L 10.293 4 L 0.5 4 C 0.224 4 0 3.776 0 3.5 C 0 3.224 0.224 3 0.5 3 L 10.293 3 L 8.146 0.854 C 7.951 0.658 7.951 0.342 8.146 0.146 Z M 3.854 7.146 C 4.049 7.342 4.049 7.658 3.854 7.854 L 1.707 10 L 11.5 10 C 11.776 10 12 10.224 12 10.5 C 12 10.776 11.776 11 11.5 11 L 1.707 11 L 3.854 13.146 C 4.049 13.342 4.049 13.658 3.854 13.854 C 3.658 14.049 3.342 14.049 3.146 13.854 L 0.146 10.854 C -0.049 10.658 -0.049 10.342 0.146 10.146 L 3.146 7.146 C 3.342 6.951 3.658 6.951 3.854 7.146 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>Change type...</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={15} height={17} viewBox="0 0 15 17" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 2,
              width: 15,
              height: 17,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 3.085 1 C 3.291 0.417 3.847 0 4.5 0 L 7.5 0 C 8.153 0 8.709 0.417 8.915 1 L 10.5 1 C 11.328 1 12 1.672 12 2.5 L 12 7.207 C 11.523 7.072 11.02 7 10.5 7 C 7.462 7 5 9.462 5 12.5 C 5 13.83 5.472 15.049 6.257 16 L 1.5 16 C 0.672 16 0 15.328 0 14.5 L 0 2.5 C 0 1.672 0.672 1 1.5 1 L 3.085 1 Z M 4.5 1 C 4.224 1 4 1.224 4 1.5 C 4 1.776 4.224 2 4.5 2 L 7.5 2 C 7.776 2 8 1.776 8 1.5 C 8 1.224 7.776 1 7.5 1 L 4.5 1 Z M 15 12.5 C 15 14.985 12.985 17 10.5 17 C 8.015 17 6 14.985 6 12.5 C 6 10.015 8.015 8 10.5 8 C 12.985 8 15 10.015 15 12.5 Z M 12.853 12.854 L 12.856 12.851 C 12.903 12.804 12.938 12.749 12.962 12.691 C 12.986 12.633 13 12.57 13 12.503 L 13 12.5 L 13 12.497 C 13 12.43 12.986 12.367 12.962 12.309 C 12.938 12.25 12.901 12.194 12.854 12.146 L 10.854 10.146 C 10.658 9.951 10.342 9.951 10.146 10.146 C 9.951 10.342 9.951 10.658 10.146 10.854 L 11.293 12 L 8.5 12 C 8.224 12 8 12.224 8 12.5 C 8 12.776 8.224 13 8.5 13 L 11.293 13 L 10.146 14.146 C 9.951 14.342 9.951 14.658 10.146 14.854 C 10.342 15.049 10.658 15.049 10.854 14.854 L 12.853 12.854 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>Move to team project</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={16} height={13} viewBox="0 0 16 13" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 4,
              width: 16,
              height: 13,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 13.5 0 C 14.881 0 16 1.119 16 2.5 L 16 10.5 C 16 11.881 14.881 13 13.5 13 L 2.5 13 C 1.119 13 0 11.881 0 10.5 L 0 2.5 C 0 1.119 1.119 0 2.5 0 L 13.5 0 Z M 15 3.961 L 8.254 7.931 C 8.123 8.008 7.967 8.02 7.828 7.969 L 7.746 7.931 L 1 3.963 L 1 10.5 C 1 11.328 1.672 12 2.5 12 L 13.5 12 C 14.328 12 15 11.328 15 10.5 L 15 3.961 Z M 13.5 1 L 2.5 1 C 1.672 1 1 1.672 1 2.5 L 1 2.802 L 8 6.92 L 15 2.801 L 15 2.5 C 15 1.672 14.328 1 13.5 1 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>Email work item</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={16} height={16.500} viewBox="0 0 16 16.500" fill="none" style={{
              position: "absolute",
              left: 2,
              top: 1.5,
              width: 16,
              height: 16.5,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 6.5 2.5 L 9.5 2.5 C 9.5 1.672 8.828 1 8 1 C 7.172 1 6.5 1.672 6.5 2.5 Z M 5.5 2.5 C 5.5 1.119 6.619 0 8 0 C 9.381 0 10.5 1.119 10.5 2.5 L 15.5 2.5 C 15.776 2.5 16 2.724 16 3 C 16 3.276 15.776 3.5 15.5 3.5 L 14.446 3.5 L 13.252 13.844 C 13.077 15.358 11.796 16.5 10.272 16.5 L 5.728 16.5 C 4.204 16.5 2.923 15.358 2.748 13.844 L 1.554 3.5 L 0.5 3.5 C 0.224 3.5 0 3.276 0 3 C 0 2.724 0.224 2.5 0.5 2.5 L 5.5 2.5 Z M 3.741 13.729 C 3.858 14.738 4.712 15.5 5.728 15.5 L 10.272 15.5 C 11.288 15.5 12.142 14.738 12.259 13.729 L 13.439 3.5 L 2.561 3.5 L 3.741 13.729 Z M 6.5 6 C 6.776 6 7 6.224 7 6.5 L 7 12.5 C 7 12.776 6.776 13 6.5 13 C 6.224 13 6 12.776 6 12.5 L 6 6.5 C 6 6.224 6.224 6 6.5 6 Z M 10 6.5 C 10 6.224 9.776 6 9.5 6 C 9.224 6 9 6.224 9 6.5 L 9 12.5 C 9 12.776 9.224 13 9.5 13 C 9.776 13 10 12.776 10 12.5 L 10 6.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>Delete</span>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--web-spacing-xs) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              padding: "0px 6px 0px 2px",
              justifyContent: "flex-end",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              flexShrink: 0,
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
              }}>Ctrl + D</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 5,
        minWidth: 120,
        maxWidth: null,
        maxHeight: 5,
        display: "flex",
        flexDirection: "row",
        padding: "8px 6px 8px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: -4,
          top: 2,
          width: 268,
          height: 1,
          backgroundColor: "var(--neutral-stroke-2-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={14} height={16} viewBox="0 0 14 16" fill="none" style={{
              position: "absolute",
              left: 3,
              top: 2,
              width: 14,
              height: 16,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.486 1.081 5.72 2.5 5.959 L 2.5 10.041 C 1.081 10.28 0 11.514 0 13 C 0 14.657 1.343 16 3 16 C 4.657 16 6 14.657 6 13 C 6 11.514 4.919 10.28 3.5 10.041 L 3.5 9 L 9 9 C 10.381 9 11.5 7.881 11.5 6.5 L 11.5 5.959 C 12.919 5.72 14 4.486 14 3 C 14 1.343 12.657 0 11 0 C 9.343 0 8 1.343 8 3 C 8 4.486 9.081 5.72 10.5 5.959 L 10.5 6.5 C 10.5 7.328 9.828 8 9 8 L 3.5 8 L 3.5 5.959 C 4.919 5.72 6 4.486 6 3 Z M 3 5 C 1.895 5 1 4.105 1 3 C 1 1.895 1.895 1 3 1 C 4.105 1 5 1.895 5 3 C 5 4.105 4.105 5 3 5 Z M 3 15 C 1.895 15 1 14.105 1 13 C 1 11.895 1.895 11 3 11 C 4.105 11 5 11.895 5 13 C 5 14.105 4.105 15 3 15 Z M 13 3 C 13 4.105 12.105 5 11 5 C 9.895 5 9 4.105 9 3 C 9 1.895 9.895 1 11 1 C 12.105 1 13 1.895 13 3 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>New branch</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 5,
        minWidth: 120,
        maxWidth: null,
        maxHeight: 5,
        display: "flex",
        flexDirection: "row",
        padding: "8px 6px 8px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "absolute",
          left: -4,
          top: 2,
          width: 268,
          height: 1,
          backgroundColor: "var(--neutral-stroke-2-rest)",
        }} />
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
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
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 3 1 C 1.895 1 1 1.895 1 3 L 1 11 C 1 12.105 1.895 13 3 13 L 11 13 C 12.105 13 13 12.105 13 11 L 13 8.5 C 13 8.224 13.224 8 13.5 8 C 13.776 8 14 8.224 14 8.5 L 14 11 C 14 12.657 12.657 14 11 14 L 3 14 C 1.343 14 0 12.657 0 11 L 0 3 C 0 1.343 1.343 0 3 0 L 5.5 0 C 5.776 0 6 0.224 6 0.5 C 6 0.776 5.776 1 5.5 1 L 3 1 Z M 8 0.5 C 8 0.224 8.224 0 8.5 0 L 13.5 0 C 13.776 0 14 0.224 14 0.5 L 14 5.5 C 14 5.776 13.776 6 13.5 6 C 13.224 6 13 5.776 13 5.5 L 13 1.707 L 8.854 5.854 C 8.658 6.049 8.342 6.049 8.146 5.854 C 7.951 5.658 7.951 5.342 8.146 5.146 L 12.293 1 L 8.5 1 C 8.224 1 8 0.776 8 0.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>Open in new tab</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        minWidth: 120,
        minHeight: 32,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--size-40) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 4,
          padding: "6px 2px 6px 6px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingRight: "calc(var(--size-20) * 1px)",
          flexGrow: 1,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            overflow: "hidden",
            flexShrink: 0,
          }}>
            <svg width={14} height={16} viewBox="0 0 14 16" fill="none" style={{
              position: "absolute",
              left: 3,
              top: 2,
              width: 14,
              height: 16,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.486 1.081 5.72 2.5 5.959 L 2.5 10.041 C 1.081 10.28 0 11.514 0 13 C 0 14.657 1.343 16 3 16 C 4.657 16 6 14.657 6 13 C 6 11.514 4.919 10.28 3.5 10.041 L 3.5 9 L 9 9 C 10.381 9 11.5 7.881 11.5 6.5 L 11.5 5.959 C 12.919 5.72 14 4.486 14 3 C 14 1.343 12.657 0 11 0 C 9.343 0 8 1.343 8 3 C 8 4.486 9.081 5.72 10.5 5.959 L 10.5 6.5 C 10.5 7.328 9.828 8 9 8 L 3.5 8 L 3.5 5.959 C 4.919 5.72 6 4.486 6 3 Z M 3 5 C 1.895 5 1 4.105 1 3 C 1 1.895 1.895 1 3 1 C 4.105 1 5 1.895 5 3 C 5 4.105 4.105 5 3 5 Z M 3 15 C 1.895 15 1 14.105 1 13 C 1 11.895 1.895 11 3 11 C 4.105 11 5 11.895 5 13 C 5 14.105 4.105 15 3 15 Z M 13 3 C 13 4.105 12.105 5 11 5 C 9.895 5 9 4.105 9 3 C 9 1.895 9.895 1 11 1 C 12.105 1 13 1.895 13 3 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
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
            }}>Add to dashboard</span>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--web-spacing-xs) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              padding: "0px 6px 0px 2px",
              justifyContent: "flex-end",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
              paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
              flexShrink: 0,
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
              }}></span>
            </div>
            <Chevron
              style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-2-rest)",
              }}
              direction={"right"}
              size={"20"}
              theme={"regular"}
            />
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingRight: "calc(var(--size-none) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 32,
            display: "flex",
            flexDirection: "column",
            gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingTop: "calc(var(--size-60) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--size-60) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 40,
              height: 20,
              overflow: "hidden",
              borderWidth: "calc(var(--stroke-width-thin-2) * 1px)",
              flexShrink: 0,
            }}>
              <div style={{
                position: "absolute",
                left: 0,
                top: 0,
                width: 40,
                height: 20,
                borderRadius: 10,
                borderTop: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderRight: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderBottom: "1px solid var(--neutral-stroke-accessible-rest-3)",
                borderLeft: "1px solid var(--neutral-stroke-accessible-rest-3)",
              }} />
              <div style={{
                position: "absolute",
                left: 3,
                top: 3,
                width: 14,
                height: 14,
                borderRadius: "50%",
                backgroundColor: "var(--neutral-stroke-accessible-rest-3)",
              }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
export default CustomMenuExample;
