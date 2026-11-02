import * as React from 'react';
/**
 * Fluent "Material acrylic" surface. Web-only: WPF has no acrylic and Slate's window surfaces are solid,
 * so don't use it in Windows app designs.
 */
export interface MaterialAcrylicProps {
  className?: string;
  style?: React.CSSProperties;
  material?: "acrylic";
}
export declare const MaterialAcrylic: React.FC<MaterialAcrylicProps>;
export default MaterialAcrylic;
