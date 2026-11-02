import * as React from 'react';
export interface QuestionCircleProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "48" | "28" | "24" | "20" | "16" | "32" | "12";
  theme?: "regular" | "filled";
}
export declare const QuestionCircle: React.FC<QuestionCircleProps>;
export default QuestionCircle;
