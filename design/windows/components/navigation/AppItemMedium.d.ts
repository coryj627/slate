import * as React from 'react';
export interface AppItemMediumProps {
  className?: string;
  style?: React.CSSProperties;
  text?: string;
  iconBefore?: React.ReactNode;
  state?: "rest" | "hover" | "pressed";
}
export declare const AppItemMedium: React.FC<AppItemMediumProps>;
export default AppItemMedium;
