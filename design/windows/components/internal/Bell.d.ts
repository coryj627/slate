import * as React from 'react';
export interface BellProps {
  className?: string;
  style?: React.CSSProperties;
  variant?: "default" | "message" | "message w dismiss";
  icon?: boolean;
  close?: boolean;
  cTA2?: boolean;
  state?: "default" | "danger";
  cTA1?: boolean;
  icon2?: React.ReactNode;
  count?: boolean;
  cTARail?: boolean;
  /** Text content; defaults to "Default messaging bell". */
  text1?: string;
  /** Text content; defaults to "1/134". */
  text2?: string;
}
export declare const Bell: React.FC<BellProps>;
export default Bell;
