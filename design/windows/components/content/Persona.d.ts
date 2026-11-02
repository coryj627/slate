import * as React from 'react';
export interface PersonaProps {
  className?: string;
  style?: React.CSSProperties;
  primaryString?: string;
  subtext?: boolean;
  tertiaryString?: string;
  secondaryString?: string;
  quaternaryText?: boolean;
  tertiaryText?: boolean;
  quaternaryString?: string;
  layout?: "text after (default)" | "text before (avatar only)" | "text below (avatar only)" | "text after" | "text before" | "text below";
  style2?: "avatar (default)" | "presence badge" | "avatar";
  /** Alias of style2 (preferred). */
  appearance?: "avatar" | "presence badge";
  size?: "small (badge only)" | "medium (default)" | "lg" | "small" | "medium" | "large";
  alignment?: "center (default)" | "top" | "center";
}
export declare const Persona: React.FC<PersonaProps>;
export default Persona;
