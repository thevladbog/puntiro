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

function tokenValue(value: TokenValue): string {
  return typeof value === 'object' ? `${value.value}${value.unit}` : String(value);
}

function collectTokens(value: TokenTree, path: string[] = []): TokenEntry[] {
  if (typeof value === 'string' || typeof value === 'number' || ('value' in value && 'unit' in value)) {
    return [{ name: path.join('.'), value: tokenValue(value) }];
  }

  return Object.entries(value).flatMap(([name, child]) => collectTokens(child, [...path, name]));
}

export function TokenGallery({ group }: { group: TokenGalleryGroup }) {
  const tokens = collectTokens(tokenGalleryGroups[group], [...tokenGalleryPaths[group]]);

  return (
    <section className="puntiro-token-gallery" aria-label={`${group} tokens`}>
      <table>
        <thead><tr><th scope="col">Token</th><th scope="col">Value</th></tr></thead>
        <tbody>
          {tokens.map((token) => (
            <tr key={token.name}><th scope="row">{token.name}</th><td>{token.value}</td></tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
