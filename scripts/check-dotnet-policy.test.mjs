import assert from 'node:assert/strict';
import { access, mkdir, mkdtemp, rm, symlink, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { after, before, test } from 'node:test';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const verifierProjectPath = path.join(
  repositoryRoot,
  'tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj',
);
const dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet';
const approvedAssemblyInfo = `using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
`;

let policyModule;
let testRoot;
let supportsFileSymlinks = false;

function runDotnet(args, cwd) {
  return spawnSync(dotnet, args, {
    cwd,
    encoding: 'utf8',
    timeout: 180_000,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
      DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1',
    },
  });
}

function commandFailure(result) {
  return [result.error?.message, result.stdout, result.stderr].filter(Boolean).join('\n');
}

function xmlAttribute(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('"', '&quot;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}

async function createFixtureRoot(name) {
  const root = path.join(testRoot, name);
  await mkdir(root, { recursive: true });
  await writeFile(path.join(root, 'global.json'), JSON.stringify({
    sdk: {
      version: '10.0.302',
      rollForward: 'disable',
      allowPrerelease: false,
    },
  }));
  await writeFile(path.join(root, 'Directory.Build.props'), `<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
`);
  return root;
}

async function writeProject(root, relativeDirectory, projectName, {
  properties = '',
  projectExtra = '',
  assemblyInfo = approvedAssemblyInfo,
  files = {},
} = {}) {
  const projectDirectory = path.join(root, relativeDirectory);
  await mkdir(path.join(projectDirectory, 'Properties'), { recursive: true });
  await writeFile(path.join(projectDirectory, `${projectName}.csproj`), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    ${properties}
  </PropertyGroup>
  ${projectExtra}
</Project>
`);
  if (assemblyInfo !== null) {
    await writeFile(path.join(projectDirectory, 'Properties/AssemblyInfo.cs'), assemblyInfo);
  }
  await writeFile(path.join(projectDirectory, 'Marker.cs'), 'public static class Marker;\n');
  await writeFile(path.join(projectDirectory, 'packages.lock.json'), JSON.stringify({
    version: 2,
    dependencies: { 'net10.0': {} },
  }));
  for (const [relativePath, content] of Object.entries(files)) {
    const target = path.join(projectDirectory, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }
  return path.posix.join(relativeDirectory, `${projectName}.csproj`);
}

function buildNormally(root, projectPath) {
  const result = runDotnet([
    'build',
    projectPath,
    '--configuration', 'Release',
    '-p:RestoreLockedMode=true',
  ], root);
  assert.equal(result.status, 0, `fixture must compile normally:\n${commandFailure(result)}`);
}

function runPolicy(root, solutionPath, protectedProjects) {
  const solutionFile = 'Puntiro.slnx';
  return writeFile(
    path.join(root, solutionFile),
    `<Solution><Project Path="${xmlAttribute(solutionPath)}" /></Solution>\n`,
  ).then(() => policyModule.runDotnetPolicy({
    dotnet,
    repositoryRoot: root,
    solutionPath: solutionFile,
    protectedProjects,
    verifierProject: {
      projectPath: verifierProjectPath,
      assemblyName: 'Puntiro.AssemblyPolicy',
    },
    projectGraph: [{ projectPath: solutionPath, references: [] }],
    logger: {},
  }));
}

before(async () => {
  policyModule = await import(new URL('./check-dotnet.mjs', import.meta.url).href);
  testRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-policy-'));
  const probeSource = path.join(testRoot, 'symlink-source');
  const probeLink = path.join(testRoot, 'symlink-link');
  await writeFile(probeSource, 'probe');
  try {
    await symlink(probeSource, probeLink, 'file');
    supportsFileSymlinks = true;
  } catch (error) {
    if (!['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) throw error;
  } finally {
    await rm(probeLink, { force: true });
    await rm(probeSource, { force: true });
  }
});

after(async () => {
  if (testRoot) await rm(testRoot, { force: true, recursive: true });
});

test('accepts relocated declarations when final identity and friend allowlist are exact', async () => {
  const root = await createFixtureRoot('relocated');
  const projectPath = await writeProject(root, '.', 'RelocatedFriends', {
    projectExtra: `<ItemGroup>
    <Compile Remove="RelocatedAssemblyInfo.cs" />
  </ItemGroup>
  <Target Name="RelocateFriendDeclarations" BeforeTargets="CoreCompile">
    <ItemGroup>
      <Compile Remove="Properties/AssemblyInfo.cs" />
      <Compile Include="RelocatedAssemblyInfo.cs" />
    </ItemGroup>
  </Target>`,
    files: {
      'RelocatedAssemblyInfo.cs': String.raw`[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Puntiro.UnitTests")]
[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Puntiro.IntegrationTests")]
`,
    },
  });

  await assert.doesNotReject(runPolicy(root, projectPath, [{
    projectPath,
    assemblyName: 'RelocatedFriends',
  }]));
});

test('accepts imported MSBuild attributes when final friend allowlist is exact', async () => {
  const root = await createFixtureRoot('imported');
  const projectPath = await writeProject(root, '.', 'ImportedFriends', {
    projectExtra: '<Import Project="Custom.props" />',
    files: {
      'Custom.props': `<Project>
  <ItemGroup>
    <Compile Remove="Properties/AssemblyInfo.cs" />
    <AssemblyAttribute Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Puntiro.UnitTests</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Puntiro.IntegrationTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
`,
    },
  });

  await assert.doesNotReject(runPolicy(root, projectPath, [{
    projectPath,
    assemblyName: 'ImportedFriends',
  }]));
});

test('rejects build success when only stale normal bin and obj exist', async () => {
  const root = await createFixtureRoot('stale');
  const projectPath = await writeProject(root, '.', 'StaleOutput');
  buildNormally(root, projectPath);
  await access(path.join(root, 'bin/Release/net10.0/StaleOutput.dll'));
  await access(path.join(root, 'obj/project.assets.json'));
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <PropertyGroup>
    <BuildDependsOn></BuildDependsOn>
  </PropertyGroup>
</Project>
`);

  await assert.rejects(
    runPolicy(root, projectPath, [{ projectPath, assemblyName: 'StaleOutput' }]),
    /isolated build did not produce expected target .*StaleOutput\.dll/,
  );
});

test('does not discover an unrelated approved normal-bin DLL when the protected output is unapproved', async () => {
  const root = await createFixtureRoot('collision');
  const protectedProject = await writeProject(root, 'Protected', 'Protected', {
    assemblyInfo: `${approvedAssemblyInfo}[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unapproved.Tests")]\n`,
  });
  const collisionProject = await writeProject(root, 'Collision', 'Collision', {
    properties: '<AssemblyName>Protected</AssemblyName>',
  });
  buildNormally(root, collisionProject);
  await access(path.join(root, 'Collision/bin/Release/net10.0/Protected.dll'));

  await assert.rejects(
    runPolicy(root, protectedProject, [{
      projectPath: protectedProject,
      assemblyName: 'Protected',
    }]),
    /Unapproved\.Tests/,
  );
});

test('accepts an approved regular-file replacement at the authoritative isolated TargetPath', async () => {
  const root = await createFixtureRoot('regular-replacement');
  const collisionProject = await writeProject(root, 'Collision', 'Collision', {
    properties: '<AssemblyName>Protected</AssemblyName>',
  });
  buildNormally(root, collisionProject);
  const approvedDll = path.join(root, 'Collision/bin/Release/net10.0/Protected.dll');
  await access(approvedDll);

  const copyScript = path.join(root, 'Protected/replace-target-with-file.mjs');
  const protectedProject = await writeProject(root, 'Protected', 'Protected', {
    assemblyInfo: `${approvedAssemblyInfo}[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unapproved.Tests")]\n`,
    projectExtra: `<Target Name="ReplaceProtectedTargetWithRegularFile" AfterTargets="Build">
    <Exec Command="&quot;${xmlAttribute(process.execPath)}&quot; &quot;${xmlAttribute(copyScript)}&quot; &quot;$(TargetPath)&quot; &quot;${xmlAttribute(approvedDll)}&quot;" />
  </Target>`,
    files: {
      'replace-target-with-file.mjs': `import { copyFileSync } from 'node:fs';

copyFileSync(process.argv[3], process.argv[2]);
`,
    },
  });

  await assert.doesNotReject(runPolicy(root, protectedProject, [{
    projectPath: protectedProject,
    assemblyName: 'Protected',
  }]));
});

test('rejects an isolated TargetPath symlink to an approved stale normal-bin DLL', async t => {
  if (!supportsFileSymlinks) {
    t.skip('file symlinks/reparse points are unavailable for the current test account');
    return;
  }

  const root = await createFixtureRoot('symlink');
  const collisionProject = await writeProject(root, 'Collision', 'Collision', {
    properties: '<AssemblyName>Protected</AssemblyName>',
  });
  buildNormally(root, collisionProject);
  const staleApprovedDll = path.join(root, 'Collision/bin/Release/net10.0/Protected.dll');
  await access(staleApprovedDll);

  const linkScript = path.join(root, 'Protected/replace-target-with-link.mjs');
  const protectedProject = await writeProject(root, 'Protected', 'Protected', {
    assemblyInfo: `${approvedAssemblyInfo}[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unapproved.Tests")]\n`,
    projectExtra: `<Target Name="ReplaceProtectedTargetWithLink" AfterTargets="Build">
    <Exec Command="&quot;${xmlAttribute(process.execPath)}&quot; &quot;${xmlAttribute(linkScript)}&quot; &quot;$(TargetPath)&quot; &quot;${xmlAttribute(staleApprovedDll)}&quot;" />
  </Target>`,
    files: {
      'replace-target-with-link.mjs': `import { rmSync, symlinkSync } from 'node:fs';

rmSync(process.argv[2], { force: true });
symlinkSync(process.argv[3], process.argv[2], 'file');
`,
    },
  });

  await assert.rejects(
    runPolicy(root, protectedProject, [{
      projectPath: protectedProject,
      assemblyName: 'Protected',
    }]),
    /symbolic link|reparse point/i,
  );
});

test('rejects a ProjectReference injected through an arbitrary imported props file', async () => {
  const root = await createFixtureRoot('imported-reference');
  const forbiddenProject = await writeProject(root, 'Forbidden', 'Forbidden');
  const protectedProject = await writeProject(root, 'Protected', 'Protected', {
    projectExtra: '<Import Project="Graph.props" />',
    files: {
      'Graph.props': `<Project>
  <ItemGroup>
    <ProjectReference Include="../${forbiddenProject}" />
  </ItemGroup>
</Project>
`,
    },
  });

  await assert.rejects(
    policyModule.validateEffectiveProjectGraph({
      dotnet,
      repositoryRoot: root,
      projectGraph: [{ projectPath: protectedProject, references: [] }],
    }),
    /unexpected effective ProjectReference.*Forbidden\/Forbidden\.csproj/,
  );
});

test('rejects a ProjectReference injected through inherited Directory.Build.targets', async () => {
  const root = await createFixtureRoot('inherited-reference');
  const forbiddenProject = await writeProject(root, 'Forbidden', 'Forbidden');
  const protectedProject = await writeProject(root, 'Protected', 'Protected');
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <Target Name="InjectInheritedReference" BeforeTargets="PrepareProjectReferences" Condition="'$(MSBuildProjectName)' == 'Protected'">
    <ItemGroup>
      <ProjectReference Include="${forbiddenProject}" />
    </ItemGroup>
  </Target>
</Project>
`);

  await assert.rejects(
    policyModule.validateEffectiveProjectGraph({
      dotnet,
      repositoryRoot: root,
      projectGraph: [{ projectPath: protectedProject, references: [] }],
    }),
    /unexpected effective ProjectReference.*Forbidden\/Forbidden\.csproj/,
  );
});

test('rejects a reference injected into the items consumed after PrepareProjectReferences', async () => {
  const root = await createFixtureRoot('late-consumed-reference');
  const forbiddenProject = await writeProject(root, 'Forbidden', 'Forbidden');
  const forbiddenProjectPath = path.join(root, forbiddenProject);
  const protectedProject = await writeProject(root, 'Protected', 'Protected');
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <Target Name="InjectConsumedReferenceAfterPreparation"
          AfterTargets="PrepareProjectReferences"
          Condition="'$(MSBuildProjectName)' == 'Protected'">
    <ItemGroup>
      <ProjectReferenceWithConfiguration Include="${xmlAttribute(forbiddenProjectPath)}"
                                         BuildReference="true"
                                         ReferenceOutputAssembly="true" />
      <_MSBuildProjectReferenceExistent Include="${xmlAttribute(forbiddenProjectPath)}"
                                        BuildReference="true"
                                        ReferenceOutputAssembly="true" />
    </ItemGroup>
  </Target>
</Project>
`);

  await assert.rejects(
    policyModule.validateEffectiveProjectGraph({
      dotnet,
      repositoryRoot: root,
      projectGraph: [{ projectPath: protectedProject, references: [] }],
    }),
    /unexpected build-consumed ProjectReference.*Forbidden\/Forbidden\.csproj/,
  );
});

test('rejects removal of a required reference immediately before ResolveProjectReferences', async () => {
  const root = await createFixtureRoot('late-removed-reference');
  const requiredProject = await writeProject(root, 'Required', 'Required');
  const protectedProject = await writeProject(root, 'Protected', 'Protected', {
    projectExtra: `<ItemGroup>
    <ProjectReference Include="../${requiredProject}" />
  </ItemGroup>`,
  });
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <Target Name="RemoveConsumedReferenceBeforeResolution"
          BeforeTargets="ResolveProjectReferences"
          Condition="'$(MSBuildProjectName)' == 'Protected'">
    <ItemGroup>
      <ProjectReferenceWithConfiguration Remove="../${requiredProject}" />
      <_MSBuildProjectReferenceExistent Remove="../${requiredProject}" />
    </ItemGroup>
  </Target>
</Project>
`);

  await assert.rejects(
    policyModule.validateEffectiveProjectGraph({
      dotnet,
      repositoryRoot: root,
      projectGraph: [
        { projectPath: requiredProject, references: [] },
        { projectPath: protectedProject, references: [requiredProject] },
      ],
    }),
    /missing build-consumed ProjectReference.*Required\/Required\.csproj/,
  );
});

test('rejects a consumed late reference hidden from item state after resolution', async () => {
  const root = await createFixtureRoot('post-resolve-hidden-reference');
  const forbiddenProject = await writeProject(root, 'Forbidden', 'Forbidden');
  const forbiddenProjectPath = path.join(root, forbiddenProject);
  const protectedProject = await writeProject(root, 'Protected', 'Protected');
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <Target Name="InjectConsumedReferenceAfterPreparation"
          AfterTargets="PrepareProjectReferences"
          Condition="'$(MSBuildProjectName)' == 'Protected'">
    <ItemGroup>
      <ProjectReferenceWithConfiguration Include="${xmlAttribute(forbiddenProjectPath)}"
                                         BuildReference="true"
                                         ReferenceOutputAssembly="true" />
      <_MSBuildProjectReferenceExistent Include="${xmlAttribute(forbiddenProjectPath)}"
                                        BuildReference="true"
                                        ReferenceOutputAssembly="true" />
    </ItemGroup>
  </Target>
  <Target Name="HideConsumedReferenceAfterResolution"
          AfterTargets="ResolveProjectReferences"
          Condition="'$(MSBuildProjectName)' == 'Protected'">
    <ItemGroup>
      <ProjectReferenceWithConfiguration Remove="${xmlAttribute(forbiddenProjectPath)}" />
      <_MSBuildProjectReferenceExistent Remove="${xmlAttribute(forbiddenProjectPath)}" />
    </ItemGroup>
  </Target>
</Project>
`);

  await assert.rejects(
    policyModule.validateEffectiveProjectGraph({
      dotnet,
      repositoryRoot: root,
      projectGraph: [{ projectPath: protectedProject, references: [] }],
    }),
    /unexpected resolved ProjectReference producer.*Forbidden\/Forbidden\.csproj/,
  );
});

test('rejects solution projects outside the exact managed graph set', async () => {
  const root = await createFixtureRoot('extra-solution-project');
  const protectedProject = await writeProject(root, 'Protected', 'Protected');
  const extraProject = await writeProject(root, 'Extra', 'Extra');
  const solutionPath = 'Puntiro.slnx';
  await writeFile(path.join(root, solutionPath), `<Solution>
  <Project Path="${protectedProject}" />
  <Project Path="${extraProject}" />
</Solution>
`);

  await assert.rejects(
    policyModule.runDotnetPolicy({
      dotnet,
      repositoryRoot: root,
      solutionPath,
      protectedProjects: [{ projectPath: protectedProject, assemblyName: 'Protected' }],
      verifierProject: {
        projectPath: verifierProjectPath,
        assemblyName: 'Puntiro.AssemblyPolicy',
      },
      projectGraph: [{ projectPath: protectedProject, references: [] }],
      logger: {},
    }),
    /unexpected solution project.*Extra\/Extra\.csproj/,
  );
});
