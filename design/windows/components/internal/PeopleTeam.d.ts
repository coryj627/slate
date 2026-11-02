import * as React from 'react';
export interface PeopleTeamProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "16" | "20" | "24" | "28" | "32" | "48";
  theme?: "regular" | "filled";
}
export declare const PeopleTeam: React.FC<PeopleTeamProps>;
export default PeopleTeam;
