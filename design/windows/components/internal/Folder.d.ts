import * as React from 'react';
export interface FolderProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "32" | "36" | "40" | "48" | "96" | "256";
  itemCount?: boolean;
  preview?: "none" | "filled" | "thumbnail";
  /** Text content; defaults to "0". */
  text1?: string;
}
export declare const Folder: React.FC<FolderProps>;
export default Folder;
