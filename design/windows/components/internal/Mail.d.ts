import * as React from 'react';
export interface MailProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "12" | "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Mail: React.FC<MailProps>;
export default Mail;
