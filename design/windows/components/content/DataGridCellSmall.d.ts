import * as React from 'react';
export interface DataGridCellSmallProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "text" | "link" | "single select" | "multi-select" | "cell actions" | "swappable" | "multi select";
  style2?: "none" | "primary" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "none" | "primary" | "secondary";
  focus?: boolean;
  avatar24px?: boolean;
  content?: string;
  productIcon?: boolean;
  productIcon24px?: React.ReactNode;
  systemIcon?: boolean;
  icon20pxReg?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const DataGridCellSmall: React.FC<DataGridCellSmallProps>;
export default DataGridCellSmall;
