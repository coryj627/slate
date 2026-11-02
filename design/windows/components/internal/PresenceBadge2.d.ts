import * as React from 'react';
export interface PresenceBadge2Props {
  className?: string;
  style?: React.CSSProperties;
  inOffice?: boolean;
  status?: "offline" | "available" | "busy" | "away" | "blocked" | "do not disturb" | "unknown" | "oof";
  size?: "extra-large huge" | "lg" | "md" | "sm" | "extra-small" | "tiny" | "extra large huge" | "large" | "medium" | "small" | "extra small";
}
export declare const PresenceBadge2: React.FC<PresenceBadge2Props>;
export default PresenceBadge2;
