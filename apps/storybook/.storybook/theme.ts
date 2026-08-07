import { create } from 'storybook/theming';

const brandIdentity = {
  colorPrimary: '#FF5A1F',
  colorSecondary: '#FF5A1F',
  appBorderRadius: 12,
  fontBase: 'Onest, Segoe UI, Arial, sans-serif',
  fontCode: 'IBM Plex Mono, Cascadia Mono, Consolas, monospace',
  brandTitle: 'Puntiro',
  brandImage: '/puntiro-mark.svg'
};

export const managerTheme = create({
  ...brandIdentity,
  base: 'dark',
  appBg: '#171914',
  appContentBg: '#171914',
  appHoverBg: '#292C26',
  appPreviewBg: '#FFFDF6',
  appBorderColor: '#C8C8BE',
  textColor: '#F2F0E8',
  textInverseColor: '#171914',
  textMutedColor: '#C8C8BE',
  barTextColor: '#F2F0E8',
  barHoverColor: '#FFFDF6',
  barSelectedColor: '#FF5A1F',
  barBg: '#171914',
  buttonBg: '#292C26',
  buttonBorder: '#C8C8BE',
  booleanBg: '#8B9189',
  booleanSelectedBg: '#FF5A1F',
  inputBg: '#292C26',
  inputBorder: '#C8C8BE',
  inputTextColor: '#FFFDF6',
  inputBorderRadius: 12
});

export const docsTheme = create({
  ...brandIdentity,
  base: 'light',
  appBg: '#F2F0E8',
  appContentBg: '#FFFDF6',
  appHoverBg: '#F2F0E8',
  appPreviewBg: '#FFFDF6',
  appBorderColor: '#C8C8BE',
  textColor: '#171914',
  textInverseColor: '#FFFDF6',
  textMutedColor: '#8B9189',
  barBg: '#F2F0E8',
  barTextColor: '#171914',
  barHoverColor: '#171914',
  barSelectedColor: '#FF5A1F',
  buttonBg: '#FFFDF6',
  buttonBorder: '#C8C8BE',
  booleanBg: '#8B9189',
  booleanSelectedBg: '#FF5A1F',
  inputBg: '#FFFDF6',
  inputBorder: '#C8C8BE',
  inputTextColor: '#171914',
  inputBorderRadius: 12
});
