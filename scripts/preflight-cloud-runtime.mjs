import { pathToFileURL } from 'node:url';
import { loadCloudEnvFiles } from './cloud-env.mjs';
import { validateCloudRuntimeMaterial } from './cloud-runtime-material.mjs';

function argumentsFrom(argv) {
  const values = new Map();
  for (let index = 0; index < argv.length; index += 2) {
    const name = argv[index];
    const value = argv[index + 1];
    if (!value || !['--compose-env', '--runtime-env'].includes(name) || values.has(name)) {
      throw new Error(
        'Usage: preflight-cloud-runtime.mjs --compose-env <path> --runtime-env <path>',
      );
    }
    values.set(name, value);
  }
  if (values.size !== 2 || !values.has('--compose-env') || !values.has('--runtime-env')) {
    throw new Error(
      'Usage: preflight-cloud-runtime.mjs --compose-env <path> --runtime-env <path>',
    );
  }
  return {
    composeEnvPath: values.get('--compose-env'),
    runtimeEnvPath: values.get('--runtime-env'),
  };
}

export async function preflightCloudRuntime(argv) {
  const { composeEnvPath, runtimeEnvPath } = argumentsFrom(argv);
  const compose = await loadCloudEnvFiles([composeEnvPath]);
  const runtime = await loadCloudEnvFiles([runtimeEnvPath]);
  await validateCloudRuntimeMaterial({ compose, composeEnvPath, runtime, runtimeEnvPath });
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  preflightCloudRuntime(process.argv.slice(2)).then(() => {
    console.log('Cloud runtime preflight passed.');
  }).catch(error => {
    console.error(error instanceof Error ? error.message : 'Cloud runtime preflight failed.');
    process.exitCode = 1;
  });
}
