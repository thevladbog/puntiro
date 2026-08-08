import { access, readFile, readdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const projects = [
  'src/Puntiro.Contracts/Puntiro.Contracts.csproj',
  'apps/cloud/Puntiro.Cloud.csproj',
  'apps/agent/Puntiro.Agent.csproj',
  'apps/kiosk-shell/Puntiro.KioskShell.csproj'
];

const projectDirectories = projects.map(project => path.posix.dirname(project));
const projectSourceExtensions = new Set(['.config', '.cs', '.csproj', '.json', '.props', '.targets', '.xaml', '.xml']);
const forbidden = ['Microsoft.Data.Sqlite', 'System.Net.Sockets', 'System.Printing', 'Microsoft.Web.WebView2'];
const uiBoundaryExtensions = new Set([
  '.cjs',
  '.config',
  '.css',
  '.html',
  '.js',
  '.json',
  '.jsx',
  '.mjs',
  '.ts',
  '.tsx',
  '.yaml',
  '.yml',
]);
const generatedUiDirectories = new Set([
  '.turbo',
  '.vite',
  'build',
  'coverage',
  'dist',
  'node_modules',
  'out',
]);

function withoutXmlComments(content) {
  return content.replace(/<!--[\s\S]*?-->/g, '');
}

function normalizedProjectPath(fromProject, include) {
  return path.posix.normalize(path.posix.join(
    path.posix.dirname(fromProject),
    include.replaceAll('\\', '/')
  ));
}

function projectReferences(projectPath, content) {
  const references = [];
  const pattern = /<ProjectReference\b[^>]*\bInclude\s*=\s*(["'])(.*?)\1[^>]*\/?\s*>/gi;
  for (const match of withoutXmlComments(content).matchAll(pattern)) {
    references.push(normalizedProjectPath(projectPath, match[2]));
  }
  return references;
}

function solutionProjects(content) {
  const entries = [];
  const pattern = /<Project\b[^>]*\bPath\s*=\s*(["'])(.*?)\1[^>]*\/?\s*>/gi;
  for (const match of withoutXmlComments(content).matchAll(pattern)) {
    entries.push(path.posix.normalize(match[2].replaceAll('\\', '/')));
  }
  return new Set(entries);
}

function sourceWithoutComments(relativePath, content) {
  if (relativePath.endsWith('.cs')) {
    return content.replace(/\/\*[\s\S]*?\*\/|\/\/.*$/gm, '');
  }
  return withoutXmlComments(content);
}

async function projectSourceFiles(root, relativeDirectory) {
  const directory = path.join(root, relativeDirectory);
  const entries = await readdir(directory, { withFileTypes: true });
  const files = await Promise.all(entries.map(async entry => {
    const relativePath = path.posix.join(relativeDirectory, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'bin' || entry.name === 'obj') return [];
      return projectSourceFiles(root, relativePath);
    }
    return projectSourceExtensions.has(path.extname(entry.name)) ? [relativePath] : [];
  }));
  return files.flat();
}

async function uiBoundaryFiles(root, relativeDirectory) {
  const directory = path.join(root, relativeDirectory);
  const entries = (await readdir(directory, { withFileTypes: true }))
    .sort((left, right) => left.name.localeCompare(right.name));
  const files = await Promise.all(entries.map(async entry => {
    const relativePath = path.posix.join(relativeDirectory, entry.name);
    if (entry.isDirectory()) {
      if (generatedUiDirectories.has(entry.name)) return [];
      return uiBoundaryFiles(root, relativePath);
    }
    const extension = path.extname(entry.name).toLowerCase();
    const isEnvironmentConfig = entry.name === '.env' || entry.name.startsWith('.env.');
    return uiBoundaryExtensions.has(extension) || isEnvironmentConfig ? [relativePath] : [];
  }));
  return files.flat();
}

export async function validateFoundation(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];
  const files = ['Puntiro.slnx', ...projects];

  for (const relativePath of files) {
    try {
      await access(path.join(root, relativePath));
    } catch {
      errors.push(`Missing foundation file: ${relativePath}`);
    }
  }
  if (errors.length > 0) return errors;

  const contents = Object.fromEntries(await Promise.all(projects.map(async relativePath => [
    relativePath,
    await readFile(path.join(root, relativePath), 'utf8')
  ])));
  const solution = await readFile(path.join(root, 'Puntiro.slnx'), 'utf8');
  const appProjects = projects.filter(relativePath => relativePath.startsWith('apps/'));
  const listedProjects = solutionProjects(solution);

  for (const relativePath of projects) {
    if (!listedProjects.has(relativePath)) {
      errors.push(`Puntiro.slnx must list project: ${relativePath}`);
    }
  }

  for (const relativePath of appProjects) {
    const references = projectReferences(relativePath, contents[relativePath]);
    if (!references.includes('src/Puntiro.Contracts/Puntiro.Contracts.csproj')) {
      errors.push(`${relativePath} must reference Puntiro.Contracts`);
    }
    for (const reference of references) {
      if (reference === 'src/Puntiro.Contracts/Puntiro.Contracts.csproj') continue;
      if (reference.startsWith('apps/')) {
        errors.push(`${relativePath} must not reference application project: ${reference}`);
      } else {
        errors.push(`${relativePath} must not reference project: ${reference}`);
      }
    }
  }
  for (const reference of projectReferences(projects[0], contents[projects[0]])) {
    if (reference.startsWith('apps/')) {
      errors.push(`Puntiro.Contracts must not reference application project: ${reference}`);
    } else {
      errors.push(`Puntiro.Contracts must not reference project: ${reference}`);
    }
  }
  if (!withoutXmlComments(contents['apps/cloud/Puntiro.Cloud.csproj']).includes('Microsoft.NET.Sdk.Web')) {
    errors.push('Cloud must use Microsoft.NET.Sdk.Web');
  }
  for (const relativePath of projects.filter(item => item !== 'apps/cloud/Puntiro.Cloud.csproj')) {
    if (withoutXmlComments(contents[relativePath]).includes('Microsoft.NET.Sdk.Web')) {
      errors.push(`${relativePath} must not use Microsoft.NET.Sdk.Web`);
    }
  }
  if (!withoutXmlComments(contents['apps/kiosk-shell/Puntiro.KioskShell.csproj']).includes('<UseWPF>true</UseWPF>')) {
    errors.push('Kiosk Shell must enable WPF');
  }

  const sourceFiles = (await Promise.all(projectDirectories.map(relativeDirectory =>
    projectSourceFiles(root, relativeDirectory)))).flat();
  for (const relativePath of sourceFiles) {
    const content = sourceWithoutComments(relativePath, await readFile(path.join(root, relativePath), 'utf8'));
    for (const marker of forbidden) {
      if (content.includes(marker)) errors.push(`${relativePath} contains deferred dependency ${marker}`);
    }
  }

  const uiApps = ['admin', 'kiosk-web'];
  const forbiddenUiMarkers = ['localhost', 'WebSocket', 'sqlite', 'net.Socket', 'window.print', '^XA', 'SIZE '];

  for (const app of uiApps) {
    const manifestPath = path.join(root, 'apps', app, 'package.json');
    const sourcePath = path.join(root, 'apps', app, 'src', 'App.tsx');
    try {
      const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
      if (manifest.private !== true) errors.push(`${app} package must be private`);
      if (manifest.dependencies?.['@puntiro/ui'] !== 'workspace:*') {
        errors.push(`${app} must consume @puntiro/ui through workspace:*`);
      }
      const source = await readFile(sourcePath, 'utf8');
      if (app === 'kiosk-web' && !source.includes('mode="touch"')) {
        errors.push('kiosk-web must use touch interaction mode');
      }
      const boundaryFiles = await uiBoundaryFiles(root, path.posix.join('apps', app));
      for (const relativePath of boundaryFiles) {
        const content = await readFile(path.join(root, relativePath), 'utf8');
        for (const marker of forbiddenUiMarkers) {
          if (content.includes(marker)) {
            errors.push(`${relativePath} contains forbidden boundary marker ${marker}`);
          }
        }
      }
    } catch {
      errors.push(`Missing product shell files for ${app}`);
    }
  }
  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateFoundation(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
