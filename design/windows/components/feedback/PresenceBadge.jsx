import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9000:575 Presence Badge (96 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "inOffice=" + __venc(p.inOffice) + '|' + "status=" + __venc(p.status) + '|' + "size=" + __venc(p.size);

export function PresenceBadge(_p = {}) {
  _p = __slateNorm(_p, {"status":["offline","available","busy","away","blocked","do not disturb","unknown","oof"],"size":["extra-large huge","lg","md","sm","extra-small","tiny"]});
  const props = { ..._p, inOffice: _p.inOffice ?? true, status: _p.status ?? "offline", size: _p.size ?? "extra-large huge" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3 0.6 C 1.675 0.6 0.6 1.675 0.6 3 C 0.6 4.325 1.675 5.4 3 5.4 C 4.325 5.4 5.4 4.325 5.4 3 C 5.4 1.675 4.325 0.6 3 0.6 Z M 0 3 C 0 1.343 1.343 0 3 0 C 4.657 0 6 1.343 6 3 C 6 4.657 4.657 6 3 6 C 1.343 6 0 4.657 0 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-oof-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3.211 2.312 C 3.328 2.194 3.328 2.004 3.21 1.887 C 3.093 1.77 2.903 1.77 2.786 1.887 L 1.887 2.788 C 1.77 2.905 1.77 3.095 1.887 3.212 L 2.786 4.112 C 2.903 4.23 3.093 4.23 3.21 4.113 C 3.328 3.996 3.328 3.806 3.211 3.688 L 2.823 3.3 L 3.903 3.3 C 4.069 3.3 4.204 3.166 4.204 3 C 4.204 2.834 4.069 2.7 3.903 2.7 L 2.823 2.7 L 3.211 2.312 Z M 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 Z M 0.6 3 C 0.6 1.675 1.675 0.6 3 0.6 C 4.325 0.6 5.4 1.675 5.4 3 C 5.4 4.325 4.325 5.4 3 5.4 C 1.675 5.4 0.6 4.325 0.6 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--neutral-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 4.112 1.888 C 4.229 2.005 4.229 2.195 4.112 2.312 L 3.424 3 L 4.112 3.688 C 4.229 3.805 4.229 3.995 4.112 4.112 C 3.995 4.229 3.805 4.229 3.688 4.112 L 3 3.424 L 2.312 4.112 C 2.195 4.229 2.005 4.229 1.888 4.112 C 1.771 3.995 1.771 3.805 1.888 3.688 L 2.576 3 L 1.888 2.312 C 1.771 2.195 1.771 2.005 1.888 1.888 C 2.005 1.771 2.195 1.771 2.312 1.888 L 3 2.576 L 3.688 1.888 C 3.805 1.771 3.995 1.771 4.112 1.888 Z M 0 3 C 0 1.343 1.343 0 3 0 C 4.657 0 6 1.343 6 3 C 6 4.657 4.657 6 3 6 C 1.343 6 0 4.657 0 3 Z M 3 0.6 C 1.675 0.6 0.6 1.675 0.6 3 C 0.6 4.325 1.675 5.4 3 5.4 C 4.325 5.4 5.4 4.325 5.4 3 C 5.4 1.675 4.325 0.6 3 0.6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 Z M 0.6 3 C 0.6 1.675 1.675 0.6 3 0.6 C 4.325 0.6 5.4 1.675 5.4 3 C 5.4 4.325 4.325 5.4 3 5.4 C 1.675 5.4 0.6 4.325 0.6 3 Z M 1.801 3.001 C 1.801 2.836 1.935 2.701 2.101 2.701 L 3.902 2.701 C 4.068 2.701 4.202 2.836 4.202 3.001 C 4.202 3.167 4.068 3.302 3.902 3.302 L 2.101 3.302 C 1.935 3.302 1.801 3.167 1.801 3.001 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 C 4.657 6 6 4.657 6 3 Z M 5.4 3 C 5.4 4.325 4.325 5.4 3 5.4 C 2.445 5.4 1.935 5.212 1.528 4.896 L 4.896 1.528 C 5.212 1.935 5.4 2.445 5.4 3 Z M 4.472 1.104 L 1.104 4.472 C 0.788 4.065 0.6 3.555 0.6 3 C 0.6 1.675 1.675 0.6 3 0.6 C 3.555 0.6 4.065 0.788 4.472 1.104 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-available-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 Z M 0.6 3 C 0.6 1.675 1.675 0.6 3 0.6 C 4.325 0.6 5.4 1.675 5.4 3 C 5.4 4.325 4.325 5.4 3 5.4 C 1.675 5.4 0.6 4.325 0.6 3 Z M 4.264 2.039 C 4.381 2.156 4.381 2.346 4.264 2.463 L 2.913 3.814 C 2.796 3.931 2.606 3.931 2.489 3.814 L 1.889 3.214 C 1.772 3.096 1.772 2.906 1.889 2.789 C 2.006 2.672 2.196 2.672 2.313 2.789 L 2.701 3.177 L 3.84 2.039 C 3.957 1.922 4.147 1.922 4.264 2.039 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 2.1 2.7 L 3.9 2.7 C 4.066 2.7 4.2 2.834 4.2 3 C 4.2 3.166 4.066 3.3 3.9 3.3 L 2.1 3.3 C 1.934 3.3 1.8 3.166 1.8 3 C 1.8 2.834 1.934 2.7 2.1 2.7 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 6 3 C 6 4.657 4.657 6 3 6 C 1.343 6 0 4.657 0 3 C 0 1.343 1.343 0 3 0 C 4.657 0 6 1.343 6 3 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-away-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 3 1.802 L 3 2.876 L 3.812 3.688 C 3.929 3.805 3.929 3.995 3.812 4.112 C 3.695 4.229 3.505 4.229 3.388 4.112 L 2.488 3.212 C 2.432 3.156 2.4 3.08 2.4 3 L 2.4 1.802 C 2.4 1.637 2.534 1.502 2.7 1.502 C 2.866 1.502 3 1.637 3 1.802 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 1px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-available-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={6} viewBox="0 0 6 6" fill="none" style={{
        position: "relative",
        width: 6,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={9.997} height={9.997} viewBox="0 0 9.997 9.997" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 9.997,
          height: 9.997,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 4.999 1 C 2.79 1 1 2.79 1 4.999 C 1 7.207 2.79 8.997 4.999 8.997 C 7.207 8.997 8.997 7.207 8.997 4.999 C 8.997 2.79 7.207 1 4.999 1 Z M 0 4.999 C 0 2.238 2.238 0 4.999 0 C 7.759 0 9.997 2.238 9.997 4.999 C 9.997 7.759 7.759 9.997 4.999 9.997 C 2.238 9.997 0 7.759 0 4.999 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 6 1.5 C 3.515 1.5 1.5 3.515 1.5 6 C 1.5 8.485 3.515 10.5 6 10.5 C 8.485 10.5 10.5 8.485 10.5 6 C 10.5 3.515 8.485 1.5 6 1.5 Z M 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={9.995} height={9.995} viewBox="0 0 9.995 9.995" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 9.995,
          height: 9.995,
          color: "var(--status-oof-foreground-3-rest-3)",
        }}>
          <path d={"M 5.349 3.851 C 5.544 3.655 5.544 3.339 5.348 3.144 C 5.153 2.949 4.836 2.949 4.641 3.144 L 3.144 4.644 C 2.949 4.84 2.949 5.156 3.144 5.351 L 4.641 6.851 C 4.836 7.046 5.153 7.047 5.348 6.851 C 5.544 6.656 5.544 6.34 5.349 6.144 L 4.703 5.498 L 6.503 5.498 C 6.779 5.498 7.003 5.274 7.003 4.998 C 7.003 4.721 6.779 4.498 6.503 4.498 L 4.703 4.498 L 5.349 3.851 Z M 4.998 0 C 2.238 0 0 2.238 0 4.998 C 0 7.758 2.238 9.995 4.998 9.995 C 7.758 9.995 9.995 7.758 9.995 4.998 C 9.995 2.238 7.758 0 4.998 0 Z M 1 4.998 C 1 2.79 2.79 1 4.998 1 C 7.206 1 8.995 2.79 8.995 4.998 C 8.995 7.206 7.206 8.995 4.998 8.995 C 2.79 8.995 1 7.206 1 4.998 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-oof-foreground-3-rest-3)",
        }}>
          <path d={"M 6.281 4.528 C 6.574 4.235 6.574 3.761 6.281 3.468 C 5.988 3.175 5.513 3.175 5.221 3.468 L 3.218 5.47 C 2.926 5.763 2.926 6.238 3.218 6.53 L 5.221 8.533 C 5.513 8.825 5.988 8.825 6.281 8.533 C 6.574 8.24 6.574 7.765 6.281 7.472 L 5.559 6.75 L 8.25 6.75 C 8.664 6.75 9 6.414 9 6 C 9 5.586 8.664 5.25 8.25 5.25 L 5.559 5.25 L 6.281 4.528 Z M 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 Z M 1.5 6 C 1.5 3.515 3.515 1.5 6 1.5 C 8.485 1.5 10.5 3.515 10.5 6 C 10.5 8.485 8.485 10.5 6 10.5 C 3.515 10.5 1.5 8.485 1.5 6 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>
          <path d={"M 6.854 3.146 C 7.049 3.342 7.049 3.658 6.854 3.854 L 5.707 5 L 6.854 6.146 C 7.049 6.342 7.049 6.658 6.854 6.854 C 6.658 7.049 6.342 7.049 6.146 6.854 L 5 5.707 L 3.854 6.854 C 3.658 7.049 3.342 7.049 3.146 6.854 C 2.951 6.658 2.951 6.342 3.146 6.146 L 4.293 5 L 3.146 3.854 C 2.951 3.658 2.951 3.342 3.146 3.146 C 3.342 2.951 3.658 2.951 3.854 3.146 L 5 4.293 L 6.146 3.146 C 6.342 2.951 6.658 2.951 6.854 3.146 Z M 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 Z M 5 1 C 2.791 1 1 2.791 1 5 C 1 7.209 2.791 9 5 9 C 7.209 9 9 7.209 9 5 C 9 2.791 7.209 1 5 1 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>
          <path d={"M 8.03 3.97 C 8.323 4.263 8.323 4.737 8.03 5.03 L 7.061 6 L 8.03 6.97 C 8.323 7.263 8.323 7.737 8.03 8.03 C 7.737 8.323 7.263 8.323 6.97 8.03 L 6 7.061 L 5.03 8.03 C 4.737 8.323 4.263 8.323 3.97 8.03 C 3.677 7.737 3.677 7.263 3.97 6.97 L 4.939 6 L 3.97 5.03 C 3.677 4.737 3.677 4.263 3.97 3.97 C 4.263 3.677 4.737 3.677 5.03 3.97 L 6 4.939 L 6.97 3.97 C 7.263 3.677 7.737 3.677 8.03 3.97 Z M 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 Z M 6 1.5 C 3.515 1.5 1.5 3.515 1.5 6 C 1.5 8.485 3.515 10.5 6 10.5 C 8.485 10.5 10.5 8.485 10.5 6 C 10.5 3.515 8.485 1.5 6 1.5 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={9.995} height={9.995} viewBox="0 0 9.995 9.995" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 9.995,
          height: 9.995,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 4.998 0 C 2.238 0 0 2.238 0 4.998 C 0 7.758 2.238 9.995 4.998 9.995 C 7.758 9.995 9.995 7.758 9.995 4.998 C 9.995 2.238 7.758 0 4.998 0 Z M 1 4.998 C 1 2.79 2.79 1 4.998 1 C 7.206 1 8.995 2.79 8.995 4.998 C 8.995 7.206 7.206 8.995 4.998 8.995 C 2.79 8.995 1 7.206 1 4.998 Z M 3 5 C 3 4.724 3.224 4.5 3.5 4.5 L 6.5 4.5 C 6.776 4.5 7 4.724 7 5 C 7 5.276 6.776 5.5 6.5 5.5 L 3.5 5.5 C 3.224 5.5 3 5.276 3 5 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 Z M 1.5 6 C 1.5 3.515 3.515 1.5 6 1.5 C 8.485 1.5 10.5 3.515 10.5 6 C 10.5 8.485 8.485 10.5 6 10.5 C 3.515 10.5 1.5 8.485 1.5 6 Z M 3 6 C 3 5.586 3.336 5.25 3.75 5.25 L 8.25 5.25 C 8.664 5.25 9 5.586 9 6 C 9 6.414 8.664 6.75 8.25 6.75 L 3.75 6.75 C 3.336 6.75 3 6.414 3 6 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 C 7.761 10 10 7.761 10 5 Z M 9 5 C 9 7.209 7.209 9 5 9 C 4.076 9 3.224 8.686 2.547 8.16 L 8.16 2.547 C 8.686 3.224 9 4.076 9 5 Z M 7.453 1.84 L 1.84 7.453 C 1.314 6.775 1 5.924 1 5 C 1 2.791 2.791 1 5 1 C 5.924 1 6.775 1.314 7.453 1.84 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 Z M 10.5 6 C 10.5 5.028 10.192 4.128 9.668 3.393 L 3.393 9.668 C 4.128 10.192 5.028 10.5 6 10.5 C 8.485 10.5 10.5 8.485 10.5 6 Z M 8.607 2.332 C 7.872 1.808 6.972 1.5 6 1.5 C 3.515 1.5 1.5 3.515 1.5 6 C 1.5 6.972 1.808 7.872 2.332 8.607 L 8.607 2.332 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={9.995} height={9.995} viewBox="0 0 9.995 9.995" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 9.995,
          height: 9.995,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 4.998 0 C 2.238 0 0 2.238 0 4.998 C 0 7.758 2.238 9.995 4.998 9.995 C 7.758 9.995 9.995 7.758 9.995 4.998 C 9.995 2.238 7.758 0 4.998 0 Z M 1 4.998 C 1 2.79 2.79 1 4.998 1 C 7.206 1 8.995 2.79 8.995 4.998 C 8.995 7.206 7.206 8.995 4.998 8.995 C 2.79 8.995 1 7.206 1 4.998 Z M 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 Z M 1.5 6 C 1.5 3.515 3.515 1.5 6 1.5 C 8.485 1.5 10.5 3.515 10.5 6 C 10.5 8.485 8.485 10.5 6 10.5 C 3.515 10.5 1.5 8.485 1.5 6 Z M 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 3.5 4.5 L 6.5 4.5 C 6.776 4.5 7 4.724 7 5 C 7 5.276 6.776 5.5 6.5 5.5 L 3.5 5.5 C 3.224 5.5 3 5.276 3 5 C 3 4.724 3.224 4.5 3.5 4.5 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 3.75 5.25 L 8.25 5.25 C 8.664 5.25 9 5.586 9 6 C 9 6.414 8.664 6.75 8.25 6.75 L 3.75 6.75 C 3.336 6.75 3 6.414 3 6 C 3 5.586 3.336 5.25 3.75 5.25 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 10 5 C 10 7.761 7.761 10 5 10 C 2.239 10 0 7.761 0 5 C 0 2.239 2.239 0 5 0 C 7.761 0 10 2.239 10 5 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 12 6 C 12 9.314 9.314 12 6 12 C 2.686 12 0 9.314 0 6 C 0 2.686 2.686 0 6 0 C 9.314 0 12 2.686 12 6 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          color: "var(--status-away-background-3-rest-3)",
        }}>
          <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 5 3.004 L 5 4.793 L 6.354 6.147 C 6.549 6.342 6.549 6.659 6.354 6.854 C 6.158 7.049 5.842 7.049 5.646 6.854 L 4.146 5.354 C 4.053 5.26 4 5.133 4 5 L 4 3.004 C 4 2.728 4.224 2.504 4.5 2.504 C 4.776 2.504 5 2.728 5 3.004 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-away-background-3-rest-3)",
        }}>
          <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6.5 3.25 L 6.5 5.655 L 7.988 6.931 C 8.303 7.2 8.339 7.674 8.069 7.988 C 7.8 8.303 7.326 8.339 7.012 8.069 L 5.262 6.569 C 5.096 6.427 5 6.219 5 6 L 5 3.25 C 5 2.836 5.336 2.5 5.75 2.5 C 6.164 2.5 6.5 2.836 6.5 3.25 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 10,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 12,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 12,
          height: 12,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 8 2 C 4.686 2 2 4.686 2 8 C 2 11.314 4.686 14 8 14 C 11.314 14 14 11.314 14 8 C 14 4.686 11.314 2 8 2 Z M 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-oof-foreground-3-rest-3)",
        }}>
          <path d={"M 8.207 6.207 C 8.598 5.817 8.598 5.184 8.207 4.793 C 7.817 4.403 7.183 4.403 6.793 4.793 L 4.293 7.293 C 4.105 7.481 4 7.735 4 8 C 4 8.265 4.105 8.52 4.293 8.707 L 6.793 11.207 C 7.183 11.598 7.817 11.598 8.207 11.207 C 8.598 10.817 8.598 10.184 8.207 9.793 L 7.414 9 L 11 9 C 11.552 9 12 8.552 12 8 C 12 7.448 11.552 7 11 7 L 7.414 7 L 8.207 6.207 Z M 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 Z M 2 8 C 2 4.686 4.686 2 8 2 C 11.314 2 14 4.686 14 8 C 14 11.314 11.314 14 8 14 C 4.686 14 2 11.314 2 8 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--neutral-foreground-3-rest-3)",
        }}>
          <path d={"M 10.707 5.293 C 11.098 5.683 11.098 6.317 10.707 6.707 L 9.414 8 L 10.707 9.293 C 11.098 9.683 11.098 10.317 10.707 10.707 C 10.317 11.098 9.683 11.098 9.293 10.707 L 8 9.414 L 6.707 10.707 C 6.317 11.098 5.683 11.098 5.293 10.707 C 4.902 10.317 4.902 9.683 5.293 9.293 L 6.586 8 L 5.293 6.707 C 4.902 6.317 4.902 5.683 5.293 5.293 C 5.683 4.902 6.317 4.902 6.707 5.293 L 8 6.586 L 9.293 5.293 C 9.683 4.902 10.317 4.902 10.707 5.293 Z M 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 Z M 8 2 C 4.686 2 2 4.686 2 8 C 2 11.314 4.686 14 8 14 C 11.314 14 14 11.314 14 8 C 14 4.686 11.314 2 8 2 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 5.25 7 C 4.698 7 4.25 7.448 4.25 8 C 4.25 8.552 4.698 9 5.25 9 L 10.75 9 C 11.302 9 11.75 8.552 11.75 8 C 11.75 7.448 11.302 7 10.75 7 L 5.25 7 Z M 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 Z M 8 2 C 4.686 2 2 4.686 2 8 C 2 11.314 4.686 14 8 14 C 11.314 14 14 11.314 14 8 C 14 4.686 11.314 2 8 2 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 Z M 14 8 C 14 6.704 13.589 5.504 12.891 4.523 L 4.523 12.891 C 5.504 13.589 6.704 14 8 14 C 11.314 14 14 11.314 14 8 Z M 11.476 3.109 C 10.496 2.411 9.296 2 8 2 C 4.686 2 2 4.686 2 8 C 2 9.296 2.411 10.496 3.109 11.476 L 11.476 3.109 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 11.707 6.707 C 12.098 6.317 12.098 5.683 11.707 5.293 C 11.317 4.902 10.683 4.902 10.293 5.293 L 7 8.586 L 5.707 7.293 C 5.317 6.902 4.683 6.902 4.293 7.293 C 3.902 7.683 3.902 8.317 4.293 8.707 L 6.293 10.707 C 6.683 11.098 7.317 11.098 7.707 10.707 L 11.707 6.707 Z M 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 Z M 8 2 C 4.686 2 2 4.686 2 8 C 2 11.314 4.686 14 8 14 C 11.314 14 14 11.314 14 8 C 14 4.686 11.314 2 8 2 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 5.249 7 L 10.75 7 C 11.302 7 11.75 7.448 11.75 8 C 11.75 8.552 11.302 9 10.75 9 L 5.249 9 C 4.697 9 4.249 8.552 4.249 8 C 4.249 7.448 4.697 7 5.249 7 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-danger-background-3-rest-3)",
        }}>
          <path d={"M 16 8 C 16 12.418 12.418 16 8 16 C 3.582 16 0 12.418 0 8 C 0 3.582 3.582 0 8 0 C 12.418 0 16 3.582 16 8 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-away-background-3-rest-3)",
        }}>
          <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8.5 4.5 L 8.5 7.519 L 10.625 9.219 C 11.056 9.564 11.126 10.193 10.781 10.625 C 10.436 11.056 9.807 11.126 9.375 10.781 L 6.875 8.781 C 6.638 8.591 6.5 8.304 6.5 8 L 6.5 4.5 C 6.5 3.948 6.948 3.5 7.5 3.5 C 8.052 3.5 8.5 3.948 8.5 4.5 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 16,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 16,
          color: "var(--status-available-foreground-3-rest-3)",
        }}>
          <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 11.707 6.707 L 7.704 10.707 C 7.516 10.895 7.261 11 6.996 11 C 6.731 11 6.477 10.894 6.289 10.707 L 4.292 8.707 C 3.902 8.316 3.903 7.683 4.293 7.292 C 4.684 6.902 5.317 6.903 5.708 7.293 L 6.998 8.586 L 10.293 5.293 C 10.684 4.902 11.317 4.902 11.707 5.293 C 12.098 5.684 12.098 6.317 11.707 6.707 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10 2.001 C 5.582 2.001 2.001 5.582 2.001 10 C 2.001 14.418 5.582 17.999 10 17.999 C 14.418 17.999 17.999 14.418 17.999 10 C 17.999 5.582 14.418 2.001 10 2.001 Z M 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-oof-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10.702 7.705 C 11.093 7.314 11.092 6.681 10.701 6.29 C 10.31 5.9 9.677 5.9 9.286 6.292 L 6.29 9.293 C 5.9 9.683 5.9 10.316 6.29 10.707 L 9.286 13.708 C 9.677 14.099 10.31 14.1 10.701 13.709 C 11.092 13.319 11.093 12.685 10.702 12.294 L 9.411 11 L 13.011 11 C 13.564 11 14.012 10.552 14.012 10 C 14.012 9.447 13.564 8.999 13.011 8.999 L 9.411 8.999 L 10.702 7.705 Z M 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 Z M 2.001 10 C 2.001 5.582 5.582 2.001 10 2.001 C 14.418 2.001 17.999 5.582 17.999 10 C 17.999 14.418 14.418 17.999 10 17.999 C 5.582 17.999 2.001 14.418 2.001 10 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--neutral-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 13.707 6.293 C 14.098 6.683 14.098 7.317 13.707 7.707 L 11.414 10 L 13.707 12.293 C 14.098 12.683 14.098 13.316 13.707 13.707 C 13.316 14.098 12.683 14.098 12.293 13.707 L 10 11.414 L 7.707 13.707 C 7.317 14.098 6.683 14.098 6.293 13.707 C 5.902 13.316 5.902 12.683 6.293 12.293 L 8.586 10 L 6.293 7.707 C 5.902 7.317 5.902 6.683 6.293 6.293 C 6.683 5.902 7.317 5.902 7.707 6.293 L 10 8.586 L 12.293 6.293 C 12.683 5.902 13.316 5.902 13.707 6.293 Z M 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 Z M 10 2 C 5.582 2 2 5.582 2 10 C 2 14.418 5.582 18 10 18 C 14.418 18 18 14.418 18 10 C 18 5.582 14.418 2 10 2 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 Z M 2.001 10 C 2.001 5.582 5.582 2.001 10 2.001 C 14.418 2.001 17.999 5.582 17.999 10 C 17.999 14.418 14.418 17.999 10 17.999 C 5.582 17.999 2.001 14.418 2.001 10 Z M 6.003 10.005 C 6.003 9.452 6.451 9.004 7.003 9.004 L 13.006 9.004 C 13.559 9.004 14.006 9.452 14.006 10.005 C 14.006 10.557 13.559 11.005 13.006 11.005 L 7.003 11.005 C 6.451 11.005 6.003 10.557 6.003 10.005 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 C 15.523 20 20 15.523 20 10 Z M 18 10 C 18 14.418 14.418 18 10 18 C 8.151 18 6.449 17.373 5.094 16.32 L 16.32 5.094 C 17.373 6.449 18 8.151 18 10 Z M 14.906 3.68 L 3.68 14.906 C 2.627 13.551 2 11.849 2 10 C 2 5.582 5.582 2 10 2 C 11.849 2 13.551 2.627 14.906 3.68 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-available-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 Z M 2.001 10 C 2.001 5.582 5.582 2.001 10 2.001 C 14.418 2.001 17.999 5.582 17.999 10 C 17.999 14.418 14.418 17.999 10 17.999 C 5.582 17.999 2.001 14.418 2.001 10 Z M 14.214 6.796 C 14.604 7.187 14.604 7.82 14.214 8.211 L 9.712 12.713 C 9.321 13.104 8.687 13.104 8.297 12.713 L 6.296 10.712 C 5.905 10.321 5.905 9.688 6.296 9.297 C 6.687 8.906 7.32 8.906 7.711 9.297 L 9.004 10.591 L 12.799 6.796 C 13.19 6.405 13.823 6.405 14.214 6.796 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 7 9 L 13 9 C 13.552 9 14 9.448 14 10 C 14 10.552 13.552 11 13 11 L 7 11 C 6.448 11 6 10.552 6 10 C 6 9.448 6.448 9 7 9 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-away-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 10 6.008 L 10 9.586 L 12.707 12.293 C 13.098 12.684 13.098 13.317 12.707 13.708 C 12.317 14.098 11.683 14.098 11.293 13.708 L 8.293 10.708 C 8.105 10.52 8 10.266 8 10 L 8 6.008 C 8 5.456 8.448 5.008 9 5.008 C 9.552 5.008 10 5.456 10 6.008 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-available-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={20} viewBox="0 0 20 20" fill="none" style={{
        position: "relative",
        width: 20,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14 2.801 C 7.815 2.801 2.801 7.815 2.801 14 C 2.801 20.185 7.815 25.199 14 25.199 C 20.185 25.199 25.199 20.185 25.199 14 C 25.199 7.815 20.185 2.801 14 2.801 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-oof-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14.983 10.787 C 15.53 10.24 15.529 9.353 14.982 8.806 C 14.434 8.26 13.547 8.261 13.001 8.808 L 8.806 13.01 C 8.26 13.557 8.26 14.442 8.806 14.989 L 13.001 19.191 C 13.547 19.739 14.434 19.739 14.982 19.193 C 15.529 18.646 15.53 17.759 14.983 17.212 L 13.175 15.4 L 18.216 15.4 C 18.99 15.4 19.617 14.773 19.617 14 C 19.617 13.226 18.99 12.599 18.216 12.599 L 13.175 12.599 L 14.983 10.787 Z M 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 Z M 2.801 14 C 2.801 7.815 7.815 2.801 14 2.801 C 20.185 2.801 25.199 7.815 25.199 14 C 25.199 20.185 20.185 25.199 14 25.199 C 7.815 25.199 2.801 20.185 2.801 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--neutral-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 19.19 8.81 C 19.737 9.357 19.737 10.243 19.19 10.79 L 15.98 14 L 19.19 17.21 C 19.737 17.757 19.737 18.643 19.19 19.19 C 18.643 19.737 17.757 19.737 17.21 19.19 L 14 15.98 L 10.79 19.19 C 10.243 19.737 9.357 19.737 8.81 19.19 C 8.263 18.643 8.263 17.757 8.81 17.21 L 12.02 14 L 8.81 10.79 C 8.263 10.243 8.263 9.357 8.81 8.81 C 9.357 8.263 10.243 8.263 10.79 8.81 L 14 12.02 L 17.21 8.81 C 17.757 8.263 18.643 8.263 19.19 8.81 Z M 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 Z M 14 2.8 C 7.814 2.8 2.8 7.814 2.8 14 C 2.8 20.186 7.814 25.2 14 25.2 C 20.186 25.2 25.2 20.186 25.2 14 C 25.2 7.814 20.186 2.8 14 2.8 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 Z M 2.801 14 C 2.801 7.815 7.815 2.801 14 2.801 C 20.185 2.801 25.199 7.815 25.199 14 C 25.199 20.185 20.185 25.199 14 25.199 C 7.815 25.199 2.801 20.185 2.801 14 Z M 8.404 14.006 C 8.404 13.233 9.031 12.606 9.805 12.606 L 18.208 12.606 C 18.982 12.606 19.609 13.233 19.609 14.006 C 19.609 14.78 18.982 15.407 18.208 15.407 L 9.805 15.407 C 9.031 15.407 8.404 14.78 8.404 14.006 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 C 21.732 28 28 21.732 28 14 Z M 25.2 14 C 25.2 20.186 20.186 25.2 14 25.2 C 11.412 25.2 9.029 24.322 7.132 22.848 L 22.848 7.132 C 24.322 9.029 25.2 11.412 25.2 14 Z M 20.868 5.152 L 5.152 20.868 C 3.678 18.971 2.8 16.588 2.8 14 C 2.8 7.814 7.814 2.8 14 2.8 C 16.588 2.8 18.971 3.678 20.868 5.152 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-available-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 Z M 2.801 14 C 2.801 7.815 7.815 2.801 14 2.801 C 20.185 2.801 25.199 7.815 25.199 14 C 25.199 20.185 20.185 25.199 14 25.199 C 7.815 25.199 2.801 20.185 2.801 14 Z M 19.899 9.514 C 20.446 10.061 20.446 10.948 19.899 11.495 L 13.596 17.798 C 13.049 18.345 12.162 18.345 11.615 17.798 L 8.814 14.997 C 8.267 14.45 8.267 13.563 8.814 13.016 C 9.361 12.469 10.248 12.469 10.795 13.016 L 12.606 14.827 L 17.918 9.514 C 18.465 8.967 19.352 8.967 19.899 9.514 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 9.8 12.6 L 18.2 12.6 C 18.973 12.6 19.6 13.227 19.6 14 C 19.6 14.773 18.973 15.4 18.2 15.4 L 9.8 15.4 C 9.027 15.4 8.4 14.773 8.4 14 C 8.4 13.227 9.027 12.6 9.8 12.6 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-danger-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 28 14 C 28 21.732 21.732 28 14 28 C 6.268 28 0 21.732 0 14 C 0 6.268 6.268 0 14 0 C 21.732 0 28 6.268 28 14 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-away-background-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 14 8.412 L 14 13.421 L 17.79 17.211 C 18.337 17.757 18.337 18.644 17.79 19.191 C 17.243 19.737 16.357 19.737 15.81 19.191 L 11.61 14.991 C 11.348 14.728 11.2 14.372 11.2 14.001 L 11.2 8.412 C 11.2 7.638 11.827 7.012 12.6 7.012 C 13.373 7.012 14 7.638 14 8.412 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "0 0 0 2px var(--neutral-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      color: "var(--status-available-foreground-3-rest-3)",
      ...props.style,
    }}>
      <svg width={28} viewBox="0 0 28 28" fill="none" style={{
        position: "relative",
        width: 28,
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <path d={"M 14 28 C 21.732 28 28 21.732 28 14 C 28 6.268 21.732 0 14 0 C 6.268 0 0 6.268 0 14 C 0 21.732 6.268 28 14 28 Z M 19.89 11.49 L 13.59 17.79 C 13.043 18.337 12.157 18.337 11.61 17.79 L 8.81 14.99 C 8.263 14.443 8.263 13.557 8.81 13.01 C 9.357 12.463 10.243 12.463 10.79 13.01 L 12.6 14.82 L 17.91 9.51 C 18.457 8.963 19.343 8.963 19.89 9.51 C 20.437 10.057 20.437 10.943 19.89 11.49 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
    </div>
  );
  const __impls = {
    // figma: In office=False, Status=Unknown, Size=Tiny
    "inOffice=false|status=unknown|size=tiny": __body0,
    // figma: In office=False, Status=OOF, Size=Tiny
    "inOffice=false|status=oof|size=tiny": __body1,
    // figma: In office=False, Status=Offline, Size=Tiny
    "inOffice=false|status=offline|size=tiny": __body2,
    // figma: In office=False, Status=Do not disturb, Size=Tiny
    "inOffice=false|status=do not disturb|size=tiny": __body3,
    // figma: In office=False, Status=Busy, Size=Tiny
    "inOffice=false|status=busy|size=tiny": __body0,
    // figma: In office=False, Status=Blocked, Size=Tiny
    "inOffice=false|status=blocked|size=tiny": __body4,
    // figma: In office=False, Status=Away, Size=Tiny
    "inOffice=false|status=away|size=tiny": __body1,
    // figma: In office=False, Status=Available, Size=Tiny
    "inOffice=false|status=available|size=tiny": __body5,
    // figma: In office=True, Status=Unknown, Size=Tiny
    "inOffice=true|status=unknown|size=tiny": __body0,
    // figma: In office=True, Status=OOF, Size=Tiny
    "inOffice=true|status=oof|size=tiny": __body1,
    // figma: In office=True, Status=Offline, Size=Tiny
    "inOffice=true|status=offline|size=tiny": __body2,
    // figma: In office=True, Status=Do not disturb, Size=Tiny
    "inOffice=true|status=do not disturb|size=tiny": __body6,
    // figma: In office=True, Status=Busy, Size=Tiny
    "inOffice=true|status=busy|size=tiny": __body7,
    // figma: In office=True, Status=Blocked, Size=Tiny
    "inOffice=true|status=blocked|size=tiny": __body4,
    // figma: In office=True, Status=Away, Size=Tiny
    "inOffice=true|status=away|size=tiny": __body8,
    // figma: In office=True, Status=Available, Size=Tiny
    "inOffice=true|status=available|size=tiny": __body9,
    // figma: In office=False, Status=Unknown, Size=Extra-small
    "inOffice=false|status=unknown|size=extra-small": __body10,
    // figma: In office=False, Status=Unknown, Size=Small
    "inOffice=false|status=unknown|size=sm": __body11,
    // figma: In office=False, Status=OOF, Size=Extra-small
    "inOffice=false|status=oof|size=extra-small": __body12,
    // figma: In office=False, Status=OOF, Size=Small
    "inOffice=false|status=oof|size=sm": __body13,
    // figma: In office=False, Status=Offline, Size=Extra-small
    "inOffice=false|status=offline|size=extra-small": __body14,
    // figma: In office=False, Status=Offline, Size=Small
    "inOffice=false|status=offline|size=sm": __body15,
    // figma: In office=False, Status=Do not disturb, Size=Extra-small
    "inOffice=false|status=do not disturb|size=extra-small": __body16,
    // figma: In office=False, Status=Do not disturb, Size=Small
    "inOffice=false|status=do not disturb|size=sm": __body17,
    // figma: In office=False, Status=Busy, Size=Extra-small
    "inOffice=false|status=busy|size=extra-small": __body10,
    // figma: In office=False, Status=Busy, Size=Small
    "inOffice=false|status=busy|size=sm": __body11,
    // figma: In office=False, Status=Blocked, Size=Extra-small
    "inOffice=false|status=blocked|size=extra-small": __body18,
    // figma: In office=False, Status=Blocked, Size=Small
    "inOffice=false|status=blocked|size=sm": __body19,
    // figma: In office=False, Status=Away, Size=Extra-small
    "inOffice=false|status=away|size=extra-small": __body12,
    // figma: In office=False, Status=Away, Size=Small
    "inOffice=false|status=away|size=sm": __body13,
    // figma: In office=False, Status=Available, Size=Extra-small
    "inOffice=false|status=available|size=extra-small": __body20,
    // figma: In office=False, Status=Available, Size=Small
    "inOffice=false|status=available|size=sm": __body21,
    // figma: In office=True, Status=Unknown, Size=Extra-small
    "inOffice=true|status=unknown|size=extra-small": __body10,
    // figma: In office=True, Status=Unknown, Size=Small
    "inOffice=true|status=unknown|size=sm": __body11,
    // figma: In office=True, Status=OOF, Size=Extra-small
    "inOffice=true|status=oof|size=extra-small": __body12,
    // figma: In office=True, Status=OOF, Size=Small
    "inOffice=true|status=oof|size=sm": __body13,
    // figma: In office=True, Status=Offline, Size=Extra-small
    "inOffice=true|status=offline|size=extra-small": __body14,
    // figma: In office=True, Status=Offline, Size=Small
    "inOffice=true|status=offline|size=sm": __body15,
    // figma: In office=True, Status=Do not disturb, Size=Extra-small
    "inOffice=true|status=do not disturb|size=extra-small": __body22,
    // figma: In office=True, Status=Do not disturb, Size=Small
    "inOffice=true|status=do not disturb|size=sm": __body23,
    // figma: In office=True, Status=Busy, Size=Extra-small
    "inOffice=true|status=busy|size=extra-small": __body24,
    // figma: In office=True, Status=Busy, Size=Small
    "inOffice=true|status=busy|size=sm": __body25,
    // figma: In office=True, Status=Blocked, Size=Extra-small
    "inOffice=true|status=blocked|size=extra-small": __body18,
    // figma: In office=True, Status=Blocked, Size=Small
    "inOffice=true|status=blocked|size=sm": __body19,
    // figma: In office=True, Status=Away, Size=Extra-small
    "inOffice=true|status=away|size=extra-small": __body26,
    // figma: In office=True, Status=Away, Size=Small
    "inOffice=true|status=away|size=sm": __body27,
    // figma: In office=True, Status=Available, Size=Extra-small
    "inOffice=true|status=available|size=extra-small": __body28,
    // figma: In office=True, Status=Available, Size=Small
    "inOffice=true|status=available|size=sm": __body29,
    // figma: In office=False, Status=Unknown, Size=Medium
    "inOffice=false|status=unknown|size=md": __body30,
    // figma: In office=False, Status=OOF, Size=Medium
    "inOffice=false|status=oof|size=md": __body31,
    // figma: In office=False, Status=Offline, Size=Medium
    "inOffice=false|status=offline|size=md": __body32,
    // figma: In office=False, Status=Do not disturb, Size=Medium
    "inOffice=false|status=do not disturb|size=md": __body33,
    // figma: In office=False, Status=Busy, Size=Medium
    "inOffice=false|status=busy|size=md": __body30,
    // figma: In office=False, Status=Blocked, Size=Medium
    "inOffice=false|status=blocked|size=md": __body34,
    // figma: In office=False, Status=Away, Size=Medium
    "inOffice=false|status=away|size=md": __body31,
    // figma: In office=False, Status=Available, Size=Medium
    "inOffice=false|status=available|size=md": __body35,
    // figma: In office=True, Status=Unknown, Size=Medium
    "inOffice=true|status=unknown|size=md": __body30,
    // figma: In office=True, Status=OOF, Size=Medium
    "inOffice=true|status=oof|size=md": __body31,
    // figma: In office=True, Status=Offline, Size=Medium
    "inOffice=true|status=offline|size=md": __body32,
    // figma: In office=True, Status=Do not disturb, Size=Medium
    "inOffice=true|status=do not disturb|size=md": __body36,
    // figma: In office=True, Status=Busy, Size=Medium
    "inOffice=true|status=busy|size=md": __body37,
    // figma: In office=True, Status=Blocked, Size=Medium
    "inOffice=true|status=blocked|size=md": __body34,
    // figma: In office=True, Status=Away, Size=Medium
    "inOffice=true|status=away|size=md": __body38,
    // figma: In office=True, Status=Available, Size=Medium
    "inOffice=true|status=available|size=md": __body39,
    // figma: In office=False, Status=Unknown, Size=Large
    "inOffice=false|status=unknown|size=lg": __body40,
    // figma: In office=False, Status=OOF, Size=Large
    "inOffice=false|status=oof|size=lg": __body41,
    // figma: In office=False, Status=Offline, Size=Large
    "inOffice=false|status=offline|size=lg": __body42,
    // figma: In office=False, Status=Do not disturb, Size=Large
    "inOffice=false|status=do not disturb|size=lg": __body43,
    // figma: In office=False, Status=Busy, Size=Large
    "inOffice=false|status=busy|size=lg": __body40,
    // figma: In office=False, Status=Blocked, Size=Large
    "inOffice=false|status=blocked|size=lg": __body44,
    // figma: In office=False, Status=Away, Size=Large
    "inOffice=false|status=away|size=lg": __body41,
    // figma: In office=False, Status=Available, Size=Large
    "inOffice=false|status=available|size=lg": __body45,
    // figma: In office=True, Status=Unknown, Size=Large
    "inOffice=true|status=unknown|size=lg": __body40,
    // figma: In office=True, Status=OOF, Size=Large
    "inOffice=true|status=oof|size=lg": __body41,
    // figma: In office=True, Status=Offline, Size=Large
    "inOffice=true|status=offline|size=lg": __body42,
    // figma: In office=True, Status=Do not disturb, Size=Large
    "inOffice=true|status=do not disturb|size=lg": __body46,
    // figma: In office=True, Status=Busy, Size=Large
    "inOffice=true|status=busy|size=lg": __body47,
    // figma: In office=True, Status=Blocked, Size=Large
    "inOffice=true|status=blocked|size=lg": __body44,
    // figma: In office=True, Status=Away, Size=Large
    "inOffice=true|status=away|size=lg": __body48,
    // figma: In office=True, Status=Available, Size=Large
    "inOffice=true|status=available|size=lg": __body49,
    // figma: In office=False, Status=Unknown, Size=Extra-large huge
    "inOffice=false|status=unknown|size=extra-large huge": __body50,
    // figma: In office=False, Status=OOF, Size=Extra-large huge
    "inOffice=false|status=oof|size=extra-large huge": __body51,
    // figma: In office=False, Status=Offline, Size=Extra-large huge
    "inOffice=false|status=offline|size=extra-large huge": __body52,
    // figma: In office=False, Status=Do not disturb, Size=Extra-large huge
    "inOffice=false|status=do not disturb|size=extra-large huge": __body53,
    // figma: In office=False, Status=Busy, Size=Extra-large huge
    "inOffice=false|status=busy|size=extra-large huge": __body50,
    // figma: In office=False, Status=Blocked, Size=Extra-large huge
    "inOffice=false|status=blocked|size=extra-large huge": __body54,
    // figma: In office=False, Status=Away, Size=Extra-large huge
    "inOffice=false|status=away|size=extra-large huge": __body51,
    // figma: In office=False, Status=Available, Size=Extra-large huge
    "inOffice=false|status=available|size=extra-large huge": __body55,
    // figma: In office=True, Status=Unknown, Size=Extra-large huge
    "inOffice=true|status=unknown|size=extra-large huge": __body50,
    // figma: In office=True, Status=OOF, Size=Extra-large huge
    "inOffice=true|status=oof|size=extra-large huge": __body51,
    // figma: In office=True, Status=Offline, Size=Extra-large huge
    "inOffice=true|status=offline|size=extra-large huge": __body52,
    // figma: In office=True, Status=Do not disturb, Size=Extra-large huge
    "inOffice=true|status=do not disturb|size=extra-large huge": __body56,
    // figma: In office=True, Status=Busy, Size=Extra-large huge
    "inOffice=true|status=busy|size=extra-large huge": __body57,
    // figma: In office=True, Status=Blocked, Size=Extra-large huge
    "inOffice=true|status=blocked|size=extra-large huge": __body54,
    // figma: In office=True, Status=Away, Size=Extra-large huge
    "inOffice=true|status=away|size=extra-large huge": __body58,
    // figma: In office=True, Status=Available, Size=Extra-large huge
    "inOffice=true|status=available|size=extra-large huge": __body59,
  };
  return (__impls[__vkey(props)] ?? __body52)();
}
export default PresenceBadge;
