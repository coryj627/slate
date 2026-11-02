import * as React from 'react';
export interface DataGridCellLargeProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "text" | "link" | "cell actions" | "single select" | "multi-select" | "two line text" | "swappable" | "multi select";
  style2?: "none" | "primary" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "none" | "primary" | "secondary";
  focus?: boolean;
  avatar32px?: boolean;
  subcontent?: string;
  productIcon?: boolean;
  productIcon32px?: React.ReactNode;
  content?: string;
  systemIcon?: boolean;
  icon20pxReg?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const DataGridCellLarge: React.FC<DataGridCellLargeProps>;
export default DataGridCellLarge;
