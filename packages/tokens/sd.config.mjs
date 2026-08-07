const toNestedResolvedDictionary = (tokens) => {
  const nested = {};

  for (const token of tokens) {
    let current = nested;

    for (const pathPart of token.path.slice(0, -1)) {
      current[pathPart] ??= {};
      current = current[pathPart];
    }

    current[token.path.at(-1)] = token.$value;
  }

  return nested;
};

export default {
  usesDtcg: true,
  source: ['src/**/*.tokens.json'],
  hooks: {
    formats: {
      'puntiro/typescript': ({ dictionary }) =>
        `export const tokens = ${JSON.stringify(toNestedResolvedDictionary(dictionary.allTokens), null, 2)} as const;\n\nexport type PuntiroTokens = typeof tokens;\n`
    }
  },
  platforms: {
    css: {
      transformGroup: 'css',
      prefix: 'puntiro',
      buildPath: 'dist/',
      files: [
        {
          destination: 'tokens.css',
          format: 'css/variables',
          options: {
            outputReferences: true,
            showFileHeader: false
          }
        }
      ]
    },
    json: {
      buildPath: 'dist/',
      files: [
        {
          destination: 'tokens.json',
          format: 'json/nested',
          options: {
            showFileHeader: false
          }
        }
      ]
    },
    typescript: {
      transformGroup: 'js',
      buildPath: 'dist/',
      files: [
        {
          destination: 'tokens.ts',
          format: 'puntiro/typescript',
          options: {
            showFileHeader: false
          }
        }
      ]
    }
  }
};
