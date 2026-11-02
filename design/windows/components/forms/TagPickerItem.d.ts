import * as React from 'react';
export interface TagPickerItemProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed";
  size?: "sm" | "medium (default)" | "small" | "medium";
  selection?: boolean;
  media?: boolean;
  iconBefore?: boolean;
  primaryString?: string;
  secondaryContent?: boolean;
  secondaryString?: string;
  endContent?: boolean;
  focus?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon3?: React.ReactNode;
}
export declare const TagPickerItem: React.FC<TagPickerItemProps>;
export default TagPickerItem;
