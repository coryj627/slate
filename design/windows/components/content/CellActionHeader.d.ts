import * as React from 'react';
export interface CellActionHeaderProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "small/smaller" | "medium/large";
  focus?: boolean;
}
export declare const CellActionHeader: React.FC<CellActionHeaderProps>;
export default CellActionHeader;
