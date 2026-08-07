import { create } from 'storybook/theming';

const sharedTheme = {
  base: 'light' as const,
  colorPrimary: '#FF5A1F',
  colorSecondary: '#FF5A1F',
  appBg: '#171914',
  appContentBg: '#F2F0E8',
  appHoverBg: '#292C26',
  appPreviewBg: '#FFFDF6',
  appBorderColor: '#C8C8BE',
  appBorderRadius: 12,
  fontBase: 'Onest, Segoe UI, Arial, sans-serif',
  fontCode: 'IBM Plex Mono, Cascadia Mono, Consolas, monospace',
  textColor: '#171914',
  textInverseColor: '#FFFDF6',
  textMutedColor: '#8B9189',
  barTextColor: '#F2F0E8',
  barHoverColor: '#FFFDF6',
  barSelectedColor: '#FF5A1F',
  barBg: '#171914',
  buttonBg: '#FFFDF6',
  buttonBorder: '#C8C8BE',
  booleanBg: '#8B9189',
  booleanSelectedBg: '#FF5A1F',
  inputBg: '#FFFDF6',
  inputBorder: '#C8C8BE',
  inputTextColor: '#171914',
  inputBorderRadius: 12,
  brandTitle: 'Puntiro',
  brandImage: '/puntiro-mark.svg'
};

export const managerTheme = create(sharedTheme);

export const docsTheme = create({
  ...sharedTheme,
  appBg: '#F2F0E8',
  appContentBg: '#FFFDF6',
  appHoverBg: '#F2F0E8',
  barBg: '#F2F0E8',
  barTextColor: '#171914',
  barHoverColor: '#171914'
});
