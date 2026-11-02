import * as React from 'react';
export interface TextProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "md" | "sm" | "lg" | "medium" | "small" | "large";
  placeholderText?: string;
  showPlaceholder?: boolean;
  enteredText?: string;
  showEnteredText?: boolean;
  showCursorAfterText?: boolean;
  showCursorBeforeText?: boolean;
}
export declare const Text: React.FC<TextProps>;
export default Text;
