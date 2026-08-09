import { lstat, readFile } from 'node:fs/promises';
import path from 'node:path';

const dotenvName = /^[A-Za-z_][A-Za-z0-9_-]*$/;

function hasInvalidValueSyntax(value) {
  return value.trim() !== value ||
    /["'`#]/.test(value) ||
    /\$(?:\(|\{|[A-Za-z_])/.test(value) ||
    /[\u0000-\u001F\u007F-\u009F\u2028\u2029]/u.test(value);
}

export async function readCloudEnvFile(filePath) {
  const metadata = await lstat(filePath);
  if (!metadata.isFile() || metadata.isSymbolicLink()) {
    throw new Error(`${path.basename(filePath)}: expected a regular env file`);
  }
  if ((metadata.mode & 0o077) !== 0) {
    throw new Error(`${path.basename(filePath)}: permissions must be 0600 or stricter`);
  }

  const values = new Map();
  const lines = (await readFile(filePath, 'utf8')).split(/\r?\n/);
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    if (line.trim().length === 0 || line.trimStart().startsWith('#')) continue;
    const separator = line.indexOf('=');
    const name = separator === -1 ? '' : line.slice(0, separator);
    if (separator === -1 || !dotenvName.test(name)) {
      throw new Error(
        `${path.basename(filePath)}:${index + 1}: invalid dotenv assignment`,
      );
    }
    const value = line.slice(separator + 1);
    if (hasInvalidValueSyntax(value)) {
      throw new Error(
        `${path.basename(filePath)}:${index + 1}: invalid dotenv value syntax`,
      );
    }
    if (values.has(name)) {
      throw new Error(
        `${path.basename(filePath)}:${index + 1}: duplicate dotenv assignment`,
      );
    }
    values.set(name, value);
  }
  return values;
}

export async function loadCloudEnvFiles(filePaths) {
  const values = new Map();
  for (const filePath of filePaths) {
    for (const [name, value] of await readCloudEnvFile(filePath)) {
      values.set(name, value);
    }
  }
  return values;
}
