import * as React from 'react';
/** Calendar date picker from the Windows UI kit: 32 px field and a 300 px calendar flyout. */
export interface DatePickerProps {
  /** Selected date, "YYYY-MM-DD". */
  value?: string;
  onChange?: (value: string) => void;
  placeholder?: string;
  /** Header above the field. */
  label?: string;
  disabled?: boolean;
  /** Shows the calendar open in place (cards and specs). */
  open?: boolean;
  /** Overrides today, "YYYY-MM-DD" (for stable mocks). */
  today?: string;
  /** 0 = Sunday (default), 1 = Monday. */
  firstDayOfWeek?: number;
  /** BCP 47 locale for month and day names; defaults to the browser's. */
  locale?: string;
  /** Field width in px (default 120). */
  width?: number;
  className?: string;
  style?: React.CSSProperties;
}
export declare function DatePicker(props: DatePickerProps): JSX.Element;
