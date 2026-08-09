import { spawn } from 'node:child_process';
import { loadCloudEnvFiles } from './cloud-env.mjs';

function argumentsFrom(argv) {
  const files = [];
  let index = 0;
  while (index < argv.length && argv[index] !== '--') {
    if (argv[index] !== '--env-file' || index + 1 >= argv.length) {
      throw new Error('Usage: run-with-cloud-env.mjs --env-file <path> [...] -- <command> [args]');
    }
    files.push(argv[index + 1]);
    index += 2;
  }
  if (files.length === 0 || argv[index] !== '--' || index + 1 >= argv.length) {
    throw new Error('Usage: run-with-cloud-env.mjs --env-file <path> [...] -- <command> [args]');
  }
  return { files, command: argv[index + 1], commandArguments: argv.slice(index + 2) };
}

async function main() {
  const { files, command, commandArguments } = argumentsFrom(process.argv.slice(2));
  const values = await loadCloudEnvFiles(files);
  const environment = { ...process.env };
  for (const [name, value] of values) environment[name] = value;

  const child = spawn(command, commandArguments, {
    env: environment,
    shell: false,
    stdio: 'inherit',
  });
  child.once('error', error => {
    console.error(`Cloud env command could not start: ${error.code ?? 'unknown error'}`);
    process.exitCode = 1;
  });
  child.once('exit', (code, signal) => {
    process.exitCode = code ?? (signal ? 1 : 0);
  });
}

main().catch(error => {
  console.error(error instanceof Error ? error.message : 'Cloud env command failed.');
  process.exitCode = 1;
});
