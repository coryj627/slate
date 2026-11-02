import * as React from 'react';
export interface ItemSplitButtonProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed" | "selected" | "disabled";
  focus?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const ItemSplitButton: React.FC<ItemSplitButtonProps>;
export default ItemSplitButton;
