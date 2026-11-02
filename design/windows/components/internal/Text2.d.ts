import * as React from 'react';
export interface Text2Props {
  className?: string;
  style?: React.CSSProperties;
  placeholderText?: string;
  showPlaceholder?: boolean;
  enteredText?: string;
  showEnteredText?: boolean;
  showCursorAfterText?: boolean;
  showCursorBeforeText?: boolean;
  size?: "md" | "sm" | "lg" | "medium" | "small" | "large";
}
export declare const Text2: React.FC<Text2Props>;
export default Text2;
