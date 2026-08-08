import { access, readdir, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const sections = ['dependencies', 'devDependencies', 'peerDependencies', 'optionalDependencies'];
const exactVersion = /^\d+\.\d+\.\d+$/;

function attributeValue(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${name}\\s*=\\s*(['"])(.*?)\\1`, 'is'));
  return match?.[2];
}

function validateNuGetVersions(centralPackages) {
  const errors = [];
  const xml = centralPackages.replace(/<!--[\s\S]*?-->/g, '');
  const declarations = xml.matchAll(
    /<PackageVersion\b([^>]*?)(?:\/\s*>|>([\s\S]*?)<\/PackageVersion\s*>)/gi,
  );

  for (const declaration of declarations) {
    const attributes = declaration[1];
    const body = declaration[2] ?? '';
    const packageName = attributeValue(attributes, 'Include')
      ?? attributeValue(attributes, 'Update')
      ?? '<unknown>';
    const childVersion = body.match(/<Version\b[^>]*>([\s\S]*?)<\/Version\s*>/i)?.[1];
    const version = (attributeValue(attributes, 'Version') ?? childVersion)?.trim();

    if (!version) {
      errors.push(`NuGet package ${packageName} is missing an exact version`);
    } else if (!exactVersion.test(version)) {
      errors.push(`NuGet package ${packageName} is not exact: ${version}`);
    }
  }

  return errors;
}

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
  const manifestEntries = await Promise.all(manifests.map(async (relativePath) => ({
    relativePath,
    manifest: JSON.parse(await readFile(path.join(root, relativePath), 'utf8')),
  })));
  const firstPartyWorkspacePackages = new Set(
    manifestEntries
      .map(({ manifest }) => manifest.name)
      .filter((name) => typeof name === 'string' && name.startsWith('@puntiro/')),
  );

  for (const { relativePath, manifest } of manifestEntries) {
    for (const section of sections) {
      for (const [name, version] of Object.entries(manifest[section] ?? {})) {
        if (version === 'workspace:*') {
          if (name.startsWith('@puntiro/') && firstPartyWorkspacePackages.has(name)) continue;
          errors.push(`${relativePath} ${section}.${name} cannot use workspace:*`);
          continue;
        }
        if (exactVersion.test(version)) continue;
        errors.push(`${relativePath} ${section}.${name} is not exact: ${version}`);
      }
    }
  }

  const rootManifest = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
  if (rootManifest.packageManager !== 'pnpm@11.17.0') {
    errors.push(`packageManager must be pnpm@11.17.0, received ${rootManifest.packageManager}`);
  }
  if (rootManifest.engines?.node !== '24.19.0') {
    errors.push(`engines.node must be 24.19.0, received ${rootManifest.engines?.node}`);
  }

  const globalJson = JSON.parse(await readFile(path.join(root, 'global.json'), 'utf8'));
  if (globalJson.sdk?.version !== '10.0.302') errors.push('global.json must pin SDK 10.0.302');
  if (globalJson.sdk?.rollForward !== 'disable') errors.push('global.json must disable rollForward');
  if (globalJson.sdk?.allowPrerelease !== false) errors.push('global.json must reject prerelease SDKs');

  const centralPackages = await readFile(path.join(root, 'Directory.Packages.props'), 'utf8');
  errors.push(...validateNuGetVersions(centralPackages));

  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateDependencyPolicy(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
