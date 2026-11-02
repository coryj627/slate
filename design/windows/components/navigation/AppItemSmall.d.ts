import * as React from 'react';
export interface AppItemSmallProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed";
  text?: string;
  iconBefore?: React.ReactNode;
}
export declare const AppItemSmall: React.FC<AppItemSmallProps>;
export default AppItemSmall;
