import * as React from 'react';
export interface DataGridCellMediumProps {
  className?: string;
  style?: React.CSSProperties;
  layout?: "cell actions" | "link" | "multi-select" | "single select" | "text" | "swappable" | "multi select";
  style2?: "none" | "primary" | "secondary";
  /** Alias of style2 (preferred). */
  appearance?: "none" | "primary" | "secondary";
  focus?: boolean;
  avatar32px?: boolean;
  content?: string;
  productIcon?: boolean;
  productIcon32px?: React.ReactNode;
  systemIcon?: boolean;
  icon20pxReg?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon2?: React.ReactNode;
}
export declare const DataGridCellMedium: React.FC<DataGridCellMediumProps>;
export default DataGridCellMedium;
