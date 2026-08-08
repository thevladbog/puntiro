import { access, readdir, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const sections = ['dependencies', 'devDependencies', 'peerDependencies', 'optionalDependencies'];
const exactVersion = /^\d+\.\d+\.\d+$/;
const nugetPolicyExtensions = new Set(['.csproj', '.props', '.targets']);
const ignoredPolicyDirectories = new Set([
  '.git',
  '.superpowers',
  'bin',
  'coverage',
  'dist',
  'node_modules',
  'obj',
]);

function attributeValue(attributes, name) {
  const match = attributes.match(new RegExp(`\\b${name}\\s*=\\s*(['"])(.*?)\\1`, 'is'));
  return match?.[2];
}

function withoutXmlComments(content) {
  return content.replace(/<!--[\s\S]*?-->/g, '');
}

function elementDeclarations(content, elementName) {
  const declarations = [];
  const pattern = new RegExp(
    `<${elementName}\\b([^>]*?)(?:\\/\\s*>|>([\\s\\S]*?)<\\/${elementName}\\s*>)`,
    'gi',
  );
  for (const match of content.matchAll(pattern)) {
    declarations.push({ attributes: match[1], body: match[2] ?? '' });
  }
  return declarations;
}

function childElement(body, elementName) {
  return new RegExp(
    `<${elementName}\\b[^>]*(?:\\/\\s*>|>[\\s\\S]*?<\\/${elementName}\\s*>)`,
    'i',
  ).test(body);
}

function childElementValue(body, elementName) {
  return body.match(
    new RegExp(`<${elementName}\\b[^>]*>([\\s\\S]*?)<\\/${elementName}\\s*>`, 'i'),
  )?.[1];
}

function propertyValues(content, propertyName) {
  const values = [];
  const pattern = new RegExp(
    `<${propertyName}\\b[^>]*>([\\s\\S]*?)<\\/${propertyName}\\s*>`,
    'gi',
  );
  for (const match of content.matchAll(pattern)) values.push(match[1].trim().toLowerCase());
  return values;
}

function declarationName(attributes) {
  return attributeValue(attributes, 'Include')
    ?? attributeValue(attributes, 'Update')
    ?? '<unknown>';
}

function validatePackageReferenceVersions(relativePath, xml) {
  const errors = [];
  for (const { attributes, body } of elementDeclarations(xml, 'PackageReference')) {
    const packageName = declarationName(attributes);
    if (attributeValue(attributes, 'Version') !== undefined || childElement(body, 'Version')) {
      errors.push(`${relativePath} PackageReference ${packageName} must not declare Version`);
    }
    if (
      attributeValue(attributes, 'VersionOverride') !== undefined
      || childElement(body, 'VersionOverride')
    ) {
      errors.push(`${relativePath} PackageReference ${packageName} must not declare VersionOverride`);
    }
  }
  return errors;
}

function validateCentralNuGetPolicy(centralPackages) {
  const errors = [];
  const xml = withoutXmlComments(centralPackages);
  const centrallyManaged = propertyValues(xml, 'ManagePackageVersionsCentrally');
  const overridesEnabled = propertyValues(xml, 'CentralPackageVersionOverrideEnabled');

  if (centrallyManaged.length !== 1 || centrallyManaged[0] !== 'true') {
    errors.push('Directory.Packages.props must set ManagePackageVersionsCentrally to true');
  }
  if (overridesEnabled.length !== 1 || overridesEnabled[0] !== 'false') {
    errors.push('Directory.Packages.props must set CentralPackageVersionOverrideEnabled to false');
  }

  const declarations = ['PackageVersion', 'GlobalPackageReference'].flatMap(elementName =>
    elementDeclarations(xml, elementName));
  for (const { attributes, body } of declarations) {
    const packageName = declarationName(attributes);
    const childVersion = childElementValue(body, 'Version');
    const version = (attributeValue(attributes, 'Version') ?? childVersion)?.trim();

    if (!version) {
      errors.push(`NuGet package ${packageName} is missing an exact version`);
    } else if (!exactVersion.test(version)) {
      errors.push(`NuGet package ${packageName} is not exact: ${version}`);
    }
  }

  errors.push(...validatePackageReferenceVersions('Directory.Packages.props', xml));

  return errors;
}

function validateNonCentralNuGetPolicy(relativePath, content) {
  const errors = [];
  const xml = withoutXmlComments(content);
  const centrallyManaged = propertyValues(xml, 'ManagePackageVersionsCentrally');
  const overridesEnabled = propertyValues(xml, 'CentralPackageVersionOverrideEnabled');

  if (centrallyManaged.some(value => value !== 'true')) {
    errors.push(`${relativePath} must not disable central package version management`);
  }
  if (overridesEnabled.some(value => value !== 'false')) {
    errors.push(`${relativePath} must not enable central package version overrides`);
  }

  errors.push(...validatePackageReferenceVersions(relativePath, xml));

  for (const elementName of ['PackageVersion', 'GlobalPackageReference']) {
    for (const { attributes } of elementDeclarations(xml, elementName)) {
      errors.push(
        `${relativePath} must not declare ${elementName} ${declarationName(attributes)} outside Directory.Packages.props`,
      );
    }
  }

  return errors;
}

async function nugetPolicyFiles(root, relativeDirectory = '') {
  const directory = path.join(root, relativeDirectory);
  const entries = (await readdir(directory, { withFileTypes: true }))
    .sort((left, right) => left.name.localeCompare(right.name));
  const files = await Promise.all(entries.map(async entry => {
    const relativePath = path.posix.join(relativeDirectory, entry.name);
    if (entry.isDirectory()) {
      if (ignoredPolicyDirectories.has(entry.name)) return [];
      return nugetPolicyFiles(root, relativePath);
    }
    return nugetPolicyExtensions.has(path.extname(entry.name).toLowerCase())
      ? [relativePath]
      : [];
  }));
  return files.flat();
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
  errors.push(...validateCentralNuGetPolicy(centralPackages));

  for (const relativePath of await nugetPolicyFiles(root)) {
    if (relativePath === 'Directory.Packages.props') continue;
    const content = await readFile(path.join(root, relativePath), 'utf8');
    errors.push(...validateNonCentralNuGetPolicy(relativePath, content));
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
