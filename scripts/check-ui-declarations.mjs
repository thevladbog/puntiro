import { readdir, readFile } from 'node:fs/promises';
import { relative, resolve } from 'node:path';

const repositoryRoot = resolve(import.meta.dirname, '..');
const declarationDirectory = resolve(process.argv[2] ?? resolve(repositoryRoot, 'packages/ui/dist'));

async function declarationFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) files.push(...await declarationFiles(path));
    if (entry.isFile() && entry.name.endsWith('.d.ts')) files.push(path);
  }

  return files;
}

const files = await declarationFiles(declarationDirectory);

if (files.length === 0) {
  console.error(`No UI declarations found in ${relative(repositoryRoot, declarationDirectory)}.`);
  process.exit(1);
}

const leakingFiles = [];
for (const file of files) {
  if ((await readFile(file, 'utf8')).includes('lucide-react')) leakingFiles.push(relative(declarationDirectory, file));
}

if (leakingFiles.length > 0) {
  console.error(`UI declarations leak lucide-react: ${leakingFiles.join(', ')}`);
  process.exit(1);
}

console.log(`UI declaration boundary passed: ${files.length} files checked.`);
