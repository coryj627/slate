import * as React from 'react';
export interface CardBodyProps {
  className?: string;
  style?: React.CSSProperties;
  bodyString?: string;
  customBody?: boolean;
}
export declare const CardBody: React.FC<CardBodyProps>;
export default CardBody;
