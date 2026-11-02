import * as React from 'react';
export interface LinkTagsProps {
  className?: string;
  style?: React.CSSProperties;
  type?: "owner name" | "partner owned" | "view documentation" | "engineering assets" | "design explorations" | "divider";
  style2?: "pill" | "text" | "divider";
  /** Alias of style2 (preferred). */
  appearance?: "pill" | "text" | "divider";
  /** Text content; defaults to "View documentation". */
  text1?: string;
}
export declare const LinkTags: React.FC<LinkTagsProps>;
export default LinkTags;
