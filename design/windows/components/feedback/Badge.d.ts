import * as React from 'react';
export interface BadgeProps {
  className?: string;
  style?: React.CSSProperties;
  color?: "brand" | "danger" | "warning" | "success" | "important" | "informative" | "subtle";
  size?: "extra large" | "lg" | "md" | "sm" | "large" | "medium" | "small";
  appearance?: "filled" | "tint" | "outline" | "subtle";
  leftIcon?: boolean;
  showText?: boolean;
  rightIcon?: boolean;
  text?: string;
}
export declare const Badge: React.FC<BadgeProps>;
export default Badge;
