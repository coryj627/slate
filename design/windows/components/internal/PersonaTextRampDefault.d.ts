import * as React from 'react';
export interface PersonaTextRampDefaultProps {
  className?: string;
  style?: React.CSSProperties;
  tertiaryText?: string;
  tertiaryTextSlot?: boolean;
  primaryText?: string;
  primaryBadgeSlot?: boolean;
  secondaryBadgeSlot?: boolean;
  secondaryTextSlot?: boolean;
  secondaryText?: string;
}
export declare const PersonaTextRampDefault: React.FC<PersonaTextRampDefaultProps>;
export default PersonaTextRampDefault;
