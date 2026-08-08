import { access, readdir, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const sections = ['dependencies', 'devDependencies', 'peerDependencies', 'optionalDependencies'];
const exactVersion = /^\d+\.\d+\.\d+$/;

async function workspaceManifests(root) {
  const manifests = ['package.json'];
  for (const parent of ['apps', 'packages']) {
    const entries = await readdir(path.join(root, parent), { withFileTypes: true });
    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      const relativePath = path.join(parent, entry.name, 'package.json');
      try {
        await access(path.join(root, relativePath));
        manifests.push(relativePath);
      } catch {
        // .NET-only app directories intentionally have no package manifest.
      }
    }
  }
  return manifests;
}

export async function validateDependencyPolicy(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];
  const manifests = await workspaceManifests(root);

  for (const relativePath of manifests) {
    const manifest = JSON.parse(await readFile(path.join(root, relativePath), 'utf8'));
    for (const section of sections) {
      for (const [name, version] of Object.entries(manifest[section] ?? {})) {
        if (version === 'workspace:*' || exactVersion.test(version)) continue;
        errors.push(`${relativePath} ${section}.${name} is not exact: ${version}`);
      }
    }
  }

  const rootManifest = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
  if (rootManifest.packageManager !== 'pnpm@11.17.0') {
    errors.push(`packageManager must be pnpm@11.17.0, received ${rootManifest.packageManager}`);
  }

  const globalJson = JSON.parse(await readFile(path.join(root, 'global.json'), 'utf8'));
  if (globalJson.sdk?.version !== '10.0.302') errors.push('global.json must pin SDK 10.0.302');
  if (globalJson.sdk?.rollForward !== 'disable') errors.push('global.json must disable rollForward');
  if (globalJson.sdk?.allowPrerelease !== false) errors.push('global.json must reject prerelease SDKs');

  const centralPackages = await readFile(path.join(root, 'Directory.Packages.props'), 'utf8');
  for (const match of centralPackages.matchAll(/<PackageVersion\s+Include="([^"]+)"\s+Version="([^"]+)"/g)) {
    if (!exactVersion.test(match[2])) errors.push(`NuGet package ${match[1]} is not exact: ${match[2]}`);
  }

  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateDependencyPolicy(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
