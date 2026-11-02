import * as React from 'react';
export interface PresenceBadge3Props {
  className?: string;
  style?: React.CSSProperties;
  inOffice?: boolean;
  status?: "offline" | "available" | "busy" | "away" | "blocked" | "do not disturb" | "unknown" | "oof";
  size?: "extra-large huge" | "lg" | "md" | "sm" | "extra-small" | "tiny" | "extra large huge" | "large" | "medium" | "small" | "extra small";
}
export declare const PresenceBadge3: React.FC<PresenceBadge3Props>;
export default PresenceBadge3;
