import * as React from 'react';
export interface MessageBarProps {
  className?: string;
  style?: React.CSSProperties;
  textString?: string;
  actions?: boolean;
  layout?: "single line (default)" | "multi line" | "single line";
  dismiss?: boolean;
  additionalAction?: boolean;
  /** Swappable nested instance; defaults to the design's. */
  icon1?: React.ReactNode;
  /** Action button labels (Slate additions). */
  actionLabel?: string;
  secondaryActionLabel?: string;
}
export declare const MessageBar: React.FC<MessageBarProps>;
export default MessageBar;
