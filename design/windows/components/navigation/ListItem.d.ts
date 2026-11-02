import * as React from 'react';
export interface ListItemProps {
  className?: string;
  style?: React.CSSProperties;
  checkmark?: boolean;
  state?: "rest" | "hover" | "pressed" | "disabled" | "focus";
  type?: "single select" | "multi select" | "header";
  text?: string;
  icon?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const ListItem: React.FC<ListItemProps>;
export default ListItem;
