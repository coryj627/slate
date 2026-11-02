import * as React from 'react';
export interface TagPickerSecondaryActionProps {
  className?: string;
  style?: React.CSSProperties;
  state?: "rest" | "hover" | "pressed";
  secondaryString?: string;
}
export declare const TagPickerSecondaryAction: React.FC<TagPickerSecondaryActionProps>;
export default TagPickerSecondaryAction;
