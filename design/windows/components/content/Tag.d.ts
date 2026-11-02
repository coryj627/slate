import * as React from 'react';
export interface TagProps {
  className?: string;
  style?: React.CSSProperties;
  avatar?: boolean;
  iconInstance16px?: React.ReactNode;
  iconInstance12px?: React.ReactNode;
  primaryString?: string;
  secondaryString?: string;
  dismiss?: boolean;
  iconBefore?: boolean;
  iconInstance20px?: React.ReactNode;
  style2?: "filled" | "outline" | "brand";
  /** Alias of style2 (preferred). */
  appearance?: "filled" | "outline" | "brand";
  size?: "md" | "sm" | "extra small" | "medium" | "small";
  radius?: "rounded square" | "circular";
  state?: "rest" | "hover (dismiss only)" | "pressed (dismiss only)" | "disabled" | "focus" | "hover" | "pressed";
  secondaryText?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const Tag: React.FC<TagProps>;
export default Tag;
