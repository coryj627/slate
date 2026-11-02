import * as React from 'react';
export interface BreadcrumbProps {
  className?: string;
  style?: React.CSSProperties;
  size?: "sm" | "md" | "lg" | "small" | "medium" | "large";
}
export declare const Breadcrumb: React.FC<BreadcrumbProps>;
export default Breadcrumb;
