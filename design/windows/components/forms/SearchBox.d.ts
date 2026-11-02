import * as React from 'react';
export interface SearchBoxProps {
  className?: string;
  /** Placeholder text; defaults to "Search". */
  placeholder?: string;
  style?: React.CSSProperties;
  style2?: "filled darker (default)" | "filled lighter" | "outline" | "transparent" | "filled darker";
  /** Alias of style2 (preferred). */
  appearance?: "filled darker" | "filled lighter" | "outline" | "transparent";
  size?: "sm" | "medium (default)" | "lg" | "small" | "medium" | "large";
  state?: "rest" | "focus" | "hover";
  iconAfter24pxRegular?: React.ReactNode;
  focusActions?: boolean;
  iconAfter12pxRegular?: React.ReactNode;
  iconAfter16pxRegular?: React.ReactNode;
  secondaryAction?: boolean;
  iconAfter212pxRegular?: React.ReactNode;
  iconAfter216pxRegular?: React.ReactNode;
  iconAfter224pxRegular?: React.ReactNode;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
}
export declare const SearchBox: React.FC<SearchBoxProps>;
export default SearchBox;
