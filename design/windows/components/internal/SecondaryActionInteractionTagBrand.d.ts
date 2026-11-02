import * as React from 'react';
export interface SecondaryActionInteractionTagBrandProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed" | "disabled";
  selected?: boolean;
  focus?: boolean;
}
export declare const SecondaryActionInteractionTagBrand: React.FC<SecondaryActionInteractionTagBrandProps>;
export default SecondaryActionInteractionTagBrand;
