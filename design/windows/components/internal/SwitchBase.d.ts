import * as React from 'react';
export interface SwitchBaseProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "switch" | "switch+vlabel" | "switch+hlabel" | "unchecked+hlabel" | "unchecked" | "unchecked+vlabel";
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const SwitchBase: React.FC<SwitchBaseProps>;
export default SwitchBase;
