import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

type TokenSource = Record<string, unknown>;

const readJson = (path: string) =>
  JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8')) as TokenSource;

const readText = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');

const approvedReferenceTokens = [
  ['reference.color.registerInk', 'color', '#171914'],
  ['reference.color.registerInkSoft', 'color', '#292C26'],
  ['reference.color.labelPaper', 'color', '#F2F0E8'],
  ['reference.color.labelPaperStrong', 'color', '#FFFDF6'],
  ['reference.color.warehouseSteel', 'color', '#8B9189'],
  ['reference.color.registrationLine', 'color', '#C8C8BE'],
  ['reference.color.handoffOrange', 'color', '#FF5A1F'],
  ['reference.color.handoffOrangeSoft', 'color', '#FFC7B3'],
  ['reference.color.statusOk', 'color', '#267A53'],
  ['reference.color.statusWarning', 'color', '#D08A00'],
  ['reference.color.statusDanger', 'color', '#C93D33'],
  ['reference.font.sans', 'fontFamily', 'Onest'],
  ['reference.font.mono', 'fontFamily', 'IBM Plex Mono'],
  ['reference.space.unit', 'dimension', { value: 8, unit: 'px' }],
  ['reference.size.touch.minimum', 'dimension', { value: 64, unit: 'px' }],
  ['reference.size.touch.comfortable', 'dimension', { value: 72, unit: 'px' }],
  ['reference.size.touch.standard', 'dimension', { value: 44, unit: 'px' }],
  ['reference.radius.label', 'dimension', { value: 4, unit: 'px' }],
  ['reference.radius.control', 'dimension', { value: 12, unit: 'px' }],
  ['reference.radius.surface', 'dimension', { value: 24, unit: 'px' }],
  ['reference.motion.fast', 'duration', '120ms'],
  ['reference.motion.confirm', 'duration', '180ms']
] as const;

const semanticAliasContracts = [
  ['product', 'semantic.color.canvas.default', '{reference.color.labelPaper}'],
  ['product', 'semantic.color.surface.default', '{reference.color.labelPaperStrong}'],
  ['product', 'semantic.color.text.primary', '{reference.color.registerInk}'],
  ['product', 'semantic.color.border.default', '{reference.color.registrationLine}'],
  ['interaction', 'semantic.color.action.primary', '{reference.color.handoffOrange}'],
  ['interaction', 'semantic.color.selected.background', '{reference.color.handoffOrangeSoft}'],
  ['interaction', 'semantic.color.focus.ring', '{reference.color.handoffOrange}'],
  ['interaction', 'semantic.color.progress.track', '{reference.color.labelPaper}'],
  ['interaction', 'semantic.color.progress.fill', '{reference.color.registerInk}'],
  ['product', 'semantic.color.success.default', '{reference.color.statusOk}'],
  ['product', 'semantic.color.warning.default', '{reference.color.statusWarning}'],
  ['product', 'semantic.color.danger.default', '{reference.color.statusDanger}'],
  ['product', 'semantic.color.disabled.background', '{reference.color.warehouseSteel}'],
  ['interaction', 'semantic.radius.label', '{reference.radius.label}'],
  ['interaction', 'semantic.radius.control', '{reference.radius.control}'],
  ['interaction', 'semantic.radius.surface', '{reference.radius.surface}'],
  ['interaction', 'semantic.space.unit', '{reference.space.unit}'],
  ['interaction', 'semantic.space.compact', '{reference.space.2}'],
  ['interaction', 'semantic.space.comfortable', '{reference.space.3}']
] as const;

const componentNames = ['Button', 'NumberInput', 'Dialog', 'ShipmentTaskCard', 'PrinterPicker'];

const componentRadiusAliases = [
  ['Button', '{semantic.radius.control}'],
  ['NumberInput', '{semantic.radius.control}'],
  ['Dialog', '{semantic.radius.surface}'],
  ['ShipmentTaskCard', '{semantic.radius.surface}'],
  ['PrinterPicker', '{semantic.radius.control}']
] as const;

const componentTokens = (component: TokenSource) => component.component as TokenSource;

describe('Puntiro token contracts', () => {
  it('keeps every approved reference value, unit, and DTCG metadata', () => {
    const sources = {
      brand: readJson('./reference/brand.tokens.json'),
      type: readJson('./reference/type.tokens.json'),
      scale: readJson('./reference/scale.tokens.json')
    };

    for (const [path, type, value] of approvedReferenceTokens) {
      const source = path.startsWith('reference.color')
        ? sources.brand
        : path.startsWith('reference.font')
          ? sources.type
          : sources.scale;

      expect(source).toHaveProperty(`${path}.$type`, type);
      expect(source).toHaveProperty(`${path}.$value`, value);
      expect(source).toHaveProperty(`${path}.$description`, expect.any(String));
    }
  });

  it('maps every required semantic role to a reference token', () => {
    const sources = {
      product: readJson('./semantic/product.tokens.json'),
      interaction: readJson('./semantic/interaction.tokens.json')
    };

    for (const [sourceName, path, alias] of semanticAliasContracts) {
      expect(sources[sourceName]).toHaveProperty(`${path}.$value`, alias);
      expect(sources[sourceName]).toHaveProperty(`${path}.$description`, expect.any(String));
    }
  });

  it('limits the first component wave to semantic aliases', () => {
    const component = readJson('./component/core.tokens.json');
    const firstWave = componentTokens(component);

    expect(Object.keys(firstWave).sort()).toEqual([...componentNames].sort());

    for (const name of componentNames) {
      const tokens = firstWave[name] as TokenSource;

      for (const token of Object.values(tokens)) {
        const tokenDefinition = token as TokenSource;
        expect(tokenDefinition.$value).toMatch(/^\{semantic\./);
        expect(tokenDefinition.$type).toEqual(expect.any(String));
        expect(tokenDefinition.$description).toEqual(expect.any(String));
      }
    }

    for (const [componentName, alias] of componentRadiusAliases) {
      expect(component).toHaveProperty(`component.${componentName}.radius.$value`, alias);
    }
  });

  it('keeps generated CSS aliases while JSON resolves the public values', () => {
    const css = readText('../dist/tokens.css');
    const json = readJson('../dist/tokens.json');

    expect(css).toContain(
      '--puntiro-semantic-color-action-primary: var(--puntiro-reference-color-handoff-orange);'
    );
    expect(css).toContain('--puntiro-component-button-radius: var(--puntiro-semantic-radius-control);');
    expect(css).toContain('--puntiro-semantic-color-progress-track: var(--puntiro-reference-color-label-paper);');
    expect(css).toContain('--puntiro-semantic-color-progress-fill: var(--puntiro-reference-color-register-ink);');
    expect(css).toContain('--puntiro-semantic-space-compact: var(--puntiro-reference-space-2);');
    expect(json).toHaveProperty('semantic.color.action.primary', '#FF5A1F');
    expect(json).toHaveProperty('semantic.color.progress.track', '#F2F0E8');
    expect(json).toHaveProperty('semantic.color.progress.fill', '#171914');
    expect(json).toHaveProperty('semantic.space.comfortable', { value: 24, unit: 'px' });
    expect(json).toHaveProperty('semantic.radius.control', { value: 12, unit: 'px' });
    expect(json).toHaveProperty('component.Button.radius', { value: 12, unit: 'px' });
  });
});
