import * as React from 'react';
export interface NavigationProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24";
  theme?: "regular" | "filled";
}
export declare const Navigation: React.FC<NavigationProps>;
export default Navigation;
