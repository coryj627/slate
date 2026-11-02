import * as React from 'react';
export interface SettingsProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled" | "light";
}
export declare const Settings: React.FC<SettingsProps>;
export default Settings;
