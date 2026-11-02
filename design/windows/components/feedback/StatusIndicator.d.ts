import * as React from 'react';
export interface StatusIndicatorProps {
  className?: string;
  style?: React.CSSProperties;
  message?: "archived" | "cancelled" | "draft" | "failed" | "inprogress" | "new" | "not started" | "pause" | "pending" | "scheduled" | "success" | "synced" | "syncing" | "unknown" | "warning" | "generic information";
  size?: "medium (default)" | "sm" | "lg" | "medium" | "small" | "large";
  category?: "informational/neutral" | "success" | "error" | "active/ in progress" | "warning";
  label?: boolean;
  genericLabel?: string;
  iconX20?: React.ReactNode;
  iconX12?: React.ReactNode;
  iconX16?: React.ReactNode;
  /** Text content; defaults to "Success". */
  text1?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const StatusIndicator: React.FC<StatusIndicatorProps>;
export default StatusIndicator;
