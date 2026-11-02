import * as React from 'react';
export interface TreeItemProps {
  className?: string;
  style?: React.CSSProperties;
  iconBefore?: boolean;
  chevron?: boolean;
  checkbox?: boolean;
  iconBeforeRegular20px?: React.ReactNode;
  iconBeforeFilled20px?: React.ReactNode;
  iconBeforeRegular16px?: React.ReactNode;
  iconBeforeFilled16px?: React.ReactNode;
  primaryText?: string;
  iconAfter?: boolean;
  iconAfter1Filled20px?: React.ReactNode;
  iconAfter2Filled20px?: React.ReactNode;
  iconAfter1Regular20px?: React.ReactNode;
  iconAfter2Regular20px?: React.ReactNode;
  iconAfter1Regular16px?: React.ReactNode;
  iconAfter2Regular16px?: React.ReactNode;
  iconAfter1Filled16px?: React.ReactNode;
  iconAfter2Filled16px?: React.ReactNode;
  badges?: boolean;
  quickActions?: boolean;
  focus?: boolean;
  style2?: "subtle (default)" | "subtle alpha" | "transparent" | "subtle";
  /** Alias of style2 (preferred). */
  appearance?: "subtle" | "subtle alpha" | "transparent";
  size?: "sm" | "medium (default)" | "small" | "medium";
  state?: "rest" | "hover" | "pressed" | "selected" | "disabled";
  leafNode?: boolean;
}
export declare const TreeItem: React.FC<TreeItemProps>;
export default TreeItem;
