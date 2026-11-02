import { MenuItem } from './MenuItem.jsx';

/* Menu flyout surface from the Windows UI kit: radius 8, 2 px vertical padding so each item's 5 px inset highlight sits
   5 px from every edge, 1 px subtle stroke, flyout shadow. Pass MenuItems as children. */
export function MenuMenu(props = {}) {
  const { className, style, customMenu, instance, children } = props;
  const sep = <div role="separator" style={{ height: 1, margin: '2px 0', background: 'var(--border-subtle)' }} />;
  const body = children ?? (customMenu && instance) ?? <>
    <MenuItem primaryString="Open in new tab" endContent endContentString="Ctrl+Enter" />
    <MenuItem primaryString="Rename" endContent endContentString="F2" state="hover" />
    <MenuItem primaryString="Move to" chevron />
    {sep}
    <MenuItem primaryString="Delete" endContent endContentString="Del" />
  </>;
  return <div role="menu" className={className} style={{ boxSizing: 'border-box', minWidth: 160, padding: '2px 0', borderRadius: 8, background: 'var(--neutral-background-1-rest)', border: '1px solid var(--border-subtle)', boxShadow: 'var(--shadow-16)', ...style }}>{body}</div>;
}
export default MenuMenu;
