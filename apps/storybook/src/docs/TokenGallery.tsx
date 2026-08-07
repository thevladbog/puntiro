import generatedTokens from '../../../../packages/tokens/dist/tokens.json';
import './start.css';

type TokenValue = string | number | { value: number; unit: string };
type TokenTree = TokenValue | { [key: string]: TokenTree };

export const tokenGalleryGroups = {
  color: generatedTokens.semantic.color,
  typography: generatedTokens.semantic.font,
  spacing: generatedTokens.reference.space,
  sizing: generatedTokens.reference.size,
  radius: generatedTokens.semantic.radius,
  motion: generatedTokens.semantic.motion,
} as const satisfies Record<string, TokenTree>;

export type TokenGalleryGroup = keyof typeof tokenGalleryGroups;

const tokenGalleryPaths: Record<TokenGalleryGroup, readonly string[]> = {
  color: ['semantic', 'color'],
  typography: ['semantic', 'font'],
  spacing: ['reference', 'space'],
  sizing: ['reference', 'size'],
  radius: ['semantic', 'radius'],
  motion: ['semantic', 'motion'],
};

interface TokenEntry {
  name: string;
  value: string;
}

const keyColorPaths = [
  'semantic.color.canvas.default',
  'semantic.color.surface.default',
  'semantic.color.text.primary',
  'semantic.color.action.primary',
  'semantic.color.selected.background',
] as const;

const statusColorPaths = [
  'semantic.color.success.default',
  'semantic.color.warning.default',
  'semantic.color.danger.default',
] as const;

function tokenValue(value: TokenValue): string {
  return typeof value === 'object' ? `${value.value}${value.unit}` : String(value);
}

function collectTokens(value: TokenTree, path: string[] = []): TokenEntry[] {
  if (typeof value === 'string' || typeof value === 'number' || ('value' in value && 'unit' in value)) {
    return [{ name: path.join('.'), value: tokenValue(value) }];
  }

  return Object.entries(value).flatMap(([name, child]) => collectTokens(child, [...path, name]));
}

function requireToken(tokens: readonly TokenEntry[], name: string): TokenEntry {
  const token = tokens.find((entry) => entry.name === name);
  if (!token) throw new Error(`Missing documented color token: ${name}`);
  return token;
}

function ColorSwatch({ token, table = false }: { token: TokenEntry; table?: boolean }) {
  return <span
    aria-hidden="true"
    className={table ? 'puntiro-token-swatch puntiro-token-swatch--table' : 'puntiro-token-swatch'}
    data-color-table-swatch={table ? 'true' : undefined}
    style={{ backgroundColor: token.value }}
  />;
}

function ColorRoleCard({ token }: { token: TokenEntry }) {
  const role = token.name.replace('semantic.color.', '');
  return <article className="puntiro-color-role" data-token-path={token.name}>
    <ColorSwatch token={token} />
    <div className="puntiro-color-role__copy">
      <strong>{role}</strong>
      <code>{token.name}</code>
      <code>{token.value}</code>
    </div>
  </article>;
}

function StatusRoleCard({ tokens }: { tokens: readonly TokenEntry[] }) {
  return <article className="puntiro-color-role puntiro-color-role--status" data-color-role-card="status">
    <div className="puntiro-color-role__status-swatches">
      {tokens.map((token) => <div key={token.name} data-token-path={token.name}>
        <ColorSwatch token={token} />
        <span>{token.name.replace('semantic.color.', '')}</span>
        <code>{token.value}</code>
      </div>)}
    </div>
  </article>;
}

function KeyColorGallery({ tokens }: { tokens: readonly TokenEntry[] }) {
  return <section className="puntiro-key-color-gallery" data-key-color-gallery aria-labelledby="puntiro-key-colors-title">
    <h2 id="puntiro-key-colors-title">Core semantic roles</h2>
    <div className="puntiro-key-color-gallery__grid">
      {keyColorPaths.map((path) => <ColorRoleCard key={path} token={requireToken(tokens, path)} />)}
      <StatusRoleCard tokens={statusColorPaths.map((path) => requireToken(tokens, path))} />
    </div>
  </section>;
}

export function TokenGallery({ group }: { group: TokenGalleryGroup }) {
  const tokens = collectTokens(tokenGalleryGroups[group], [...tokenGalleryPaths[group]]);
  const isColor = group === 'color';

  return (
    <section className="puntiro-token-gallery" aria-label={`${group} tokens`}>
      {isColor ? <KeyColorGallery tokens={tokens} /> : null}
      <table>
        <thead><tr>
          {isColor ? <th scope="col">Preview</th> : null}
          <th scope="col">Token</th><th scope="col">Value</th>
        </tr></thead>
        <tbody>
          {tokens.map((token) => (
            <tr key={token.name}>
              {isColor ? <td><ColorSwatch token={token} table /></td> : null}
              <th scope="row">{token.name}</th><td>{token.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
