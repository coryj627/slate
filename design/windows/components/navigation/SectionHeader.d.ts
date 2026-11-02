import * as React from 'react';
export interface SectionHeaderProps {
  className?: string;
  style?: React.CSSProperties;
  /** Text content; defaults to "Section header". */
  text1?: string;
}
export declare const SectionHeader: React.FC<SectionHeaderProps>;
export default SectionHeader;
