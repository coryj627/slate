import * as React from 'react';
export interface AnatomyBadgeProps {
  className?: string;
  style?: React.CSSProperties;
  style2?: "standard" | "subtle" | "link";
  /** Alias of style2 (preferred). */
  appearance?: "standard" | "subtle" | "link";
  /** Text content; defaults to "Badge". */
  text1?: string;
}
export declare const AnatomyBadge: React.FC<AnatomyBadgeProps>;
export default AnatomyBadge;
