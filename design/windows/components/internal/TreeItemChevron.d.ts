import * as React from 'react';
export interface TreeItemChevronProps {
  className?: string;
  style?: React.CSSProperties;
  expanded?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const TreeItemChevron: React.FC<TreeItemChevronProps>;
export default TreeItemChevron;
