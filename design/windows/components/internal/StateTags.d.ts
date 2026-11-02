import * as React from 'react';
export interface StateTagsProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "visionary feature" | "experimental feature" | "establishing pattern" | "coherent pattern" | "local feature" | "incoherent feature";
  /** Text content; defaults to "Visionary feature". */
  text1?: string;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const StateTags: React.FC<StateTagsProps>;
export default StateTags;
