import * as React from 'react';
export interface DataGridCellSmallerProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "text" | "link" | "cell actions" | "single select" | "multi-select" | "swappable" | "multi select";
  style2?: "none" | "primary" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "none" | "primary" | "secondary";
  focus?: boolean;
  avatar20px?: boolean;
  content?: string;
  productIcon?: boolean;
  productIcon20px?: React.ReactNode;
  systemIcon?: boolean;
  icon20pxReg?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const DataGridCellSmaller: React.FC<DataGridCellSmallerProps>;
export default DataGridCellSmaller;
