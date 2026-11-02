import * as React from 'react';
export interface SecondaryActionProps {
  className?: string;
  style?: React.CSSProperties;
  focus?: boolean;
  style2?: "primary" | "secondary (default)" | "outline" | "subtle" | "transparent" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "primary" | "secondary" | "outline" | "subtle" | "transparent";
  state?: "rest" | "hover" | "pressed" | "selected" | "disabled";
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const SecondaryAction: React.FC<SecondaryActionProps>;
export default SecondaryAction;
