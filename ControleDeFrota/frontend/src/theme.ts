import { createTheme, type MantineColorsTuple } from '@mantine/core';

/**
 * Single source of colors, radii and fonts (UX_UI.md). Brand red is used sparingly:
 * primary action, active navigation and focus. Everything else stays neutral.
 */
const brand: MantineColorsTuple = [
  '#fff0f1',
  '#ffdfe1',
  '#fbbdc1',
  '#f6989e',
  '#f1777f',
  '#ee626b',
  '#ed5660',
  '#d34650',
  '#c92a37',
  '#a61e2b',
];

export const theme = createTheme({
  primaryColor: 'brand',
  primaryShade: { light: 8, dark: 6 },
  colors: { brand },
  fontFamily: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
  headings: { fontFamily: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif', fontWeight: '650' },
  defaultRadius: 'md',
  cursorType: 'pointer',
  focusRing: 'auto',
  components: {
    Button: { defaultProps: { fw: 550 } },
    Paper: { defaultProps: { withBorder: true, shadow: 'xs' } },
    Card: { defaultProps: { withBorder: true, shadow: 'xs' } },
    TextInput: { defaultProps: { size: 'sm' } },
    Select: { defaultProps: { allowDeselect: false, comboboxProps: { shadow: 'md' } } },
    Badge: { defaultProps: { variant: 'light', radius: 'sm' } },
    Tooltip: { defaultProps: { withArrow: true } },
  },
});

/** Badge colors by meaning — used by every status map so the same meaning always has the same color. */
export const STATUS_COLOR = {
  positive: 'teal',
  active: 'blue',
  warning: 'orange',
  neutral: 'gray',
  danger: 'red',
} as const;
