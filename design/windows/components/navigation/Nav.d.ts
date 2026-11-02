import * as React from 'react';
export interface NavProps {
  className?: string;
  style?: React.CSSProperties;
  navBody?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const Nav: React.FC<NavProps>;
export default Nav;
