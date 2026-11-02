import * as React from 'react';
export interface CardProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "default" | "custom";
  state?: "rest" | "hover" | "pressed" | "draggable" | "selected" | "disabled";
  style2?: "filled" | "filled alt" | "outline" | "subtle";
  /** Alias of style2 (preferred). */
  appearance?: "filled" | "filled alt" | "outline" | "subtle";
  body?: boolean;
  footer?: boolean;
}
export declare const Card: React.FC<CardProps>;
export default Card;
