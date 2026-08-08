# Puntiro Repository Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Превратить существующую дизайн-систему в проверяемый product monorepo foundation с обязательным `AGENTS.md`, documentation-as-code, точной dependency policy, .NET solution, React application shells и базовым GitHub CI.

**Architecture:** Существующий pnpm workspace остаётся источником React UI и Storybook. Рядом создаётся .NET 10 solution с Cloud, Agent, Contracts и Windows Kiosk Shell boundaries; Node contract tests проверяют структуру до появления полноценных subsystem test projects. Foundation не реализует доменную логику, синхронизацию или печать.

**Tech Stack:** Node.js 24.19.0 LTS, pnpm 11.17.0, TypeScript 6.0.3, React 19.2.8, Vite 8.1.5, .NET SDK 10.0.302, ASP.NET Core 10, WPF, Node built-in test runner, GitHub Actions.

## Global Constraints

- Перед добавлением пакета сначала разрешить официальный Context7 library ID и прочитать актуальную документацию; затем проверить official security advisories.
- Все registry npm/NuGet/tool versions exact; pre-release и floating ranges запрещены. `workspace:*` разрешён только для first-party `@puntiro/*` workspace packages.
- Существующие `packages/ui`, `packages/tokens`, Storybook tests и visual baselines не изменять без прямой необходимости foundation.
- `AGENTS.md` создаётся до production-кода и обновляется вместе с реальными командами.
- Documentation-as-code: README, docs map и ADR входят в тот же change set, что и foundation.
- Не создавать business API, SQLite schema, printer transport, OAuth, sync или kiosk product screens в этом плане.
- Не считать macOS/Linux build доказательством Windows Service, WPF или hardware acceptance.
- Сохранять пользовательский `.pnpm-store/` untracked и не добавлять его в commit.

---

## File Structure

```text
AGENTS.md                                      repository agent contract
README.md                                      product-oriented entrypoint
docs/README.md                                 documentation map
docs/adr/0001-modular-monolith-agent.md        accepted architecture decision
docs/runbooks/development-bootstrap.md         reproducible local bootstrap
scripts/check-docs.mjs                         documentation contract checker
scripts/check-docs.test.mjs                    checker tests
scripts/check-dependency-policy.mjs            exact-version policy checker
scripts/check-dependency-policy.test.mjs       policy tests
scripts/check-foundation.mjs                   cross-stack structure checker
scripts/check-foundation.test.mjs              foundation contract tests
global.json                                    exact .NET SDK pin
Directory.Build.props                          shared .NET compiler policy
Directory.Packages.props                       central NuGet version ownership
Puntiro.slnx                                   .NET solution manifest
src/Puntiro.Contracts/                         shared IPC protocol boundary only
apps/cloud/                                    empty ASP.NET host boundary
apps/agent/                                    empty service-process boundary
apps/kiosk-shell/                              empty WPF/WebView2 host boundary
apps/admin/                                    empty React admin shell
apps/kiosk-web/                                empty React kiosk shell
.github/workflows/foundation.yml               Linux and Windows foundation CI
```

---

### Task 1: Repository Operating Contract and Documentation Map

**Files:**
- Create: `AGENTS.md`
- Modify: `README.md`
- Create: `docs/README.md`
- Create: `docs/adr/0001-modular-monolith-agent.md`
- Create: `docs/runbooks/development-bootstrap.md`
- Create: `scripts/check-docs.test.mjs`
- Create: `scripts/check-docs.mjs`
- Modify: `package.json`

**Interfaces:**
- Consumes: approved technical architecture at `docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md`.
- Produces: `node scripts/check-docs.mjs`; root instructions that every later task must obey.

- [ ] **Step 1: Write the failing documentation contract test**

Create `scripts/check-docs.test.mjs`:

```js
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateRepositoryDocs } from './check-docs.mjs';

test('required repository documentation is complete', async () => {
  const errors = await validateRepositoryDocs(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});
```

- [ ] **Step 2: Run the test to verify RED**

Run:

```bash
node --test scripts/check-docs.test.mjs
```

Expected: FAIL with `ERR_MODULE_NOT_FOUND` for `scripts/check-docs.mjs`.

- [ ] **Step 3: Implement the documentation checker**

Create `scripts/check-docs.mjs`:

```js
import { access, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const requiredFiles = [
  'AGENTS.md',
  'README.md',
  'docs/README.md',
  'docs/adr/0001-modular-monolith-agent.md',
  'docs/runbooks/development-bootstrap.md',
  'docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md'
];

const agentsHeadings = [
  '# Puntiro Repository Instructions',
  '## Architecture Invariants',
  '## Required Commands',
  '## Dependency Policy',
  '## Documentation Policy',
  '## Validation Boundaries',
  '## Secrets and Logs',
  '## Git Safety'
];

export async function validateRepositoryDocs(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];

  for (const relativePath of requiredFiles) {
    try {
      await access(path.join(root, relativePath));
    } catch {
      errors.push(`Missing documentation: ${relativePath}`);
    }
  }

  try {
    const agents = await readFile(path.join(root, 'AGENTS.md'), 'utf8');
    for (const heading of agentsHeadings) {
      if (!agents.includes(heading)) errors.push(`AGENTS.md missing heading: ${heading}`);
    }
  } catch {
    // Missing-file diagnostic is already emitted above.
  }

  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateRepositoryDocs(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
```

- [ ] **Step 4: Create `AGENTS.md` with concrete repository rules**

Create `AGENTS.md` with these exact top-level sections and requirements:

```markdown
# Puntiro Repository Instructions

Read this file before changing the repository. The closest nested `AGENTS.md` may add stricter local rules but cannot weaken this file.

## Architecture Invariants

- Cloud owns shipment intent, configuration, and durable audit.
- Windows Agent owns SQLite, offline grace, label sets, and physical print attempts.
- React UI never opens SQLite, holds device credentials, or talks to printers.
- Printing is forbidden before a durable local transaction stores exact payload bytes.
- `unknown` print results are never retried or archived automatically.

## Required Commands

- Bootstrap: `corepack pnpm install --frozen-lockfile`
- Documentation: `corepack pnpm docs:check`
- Existing UI pipeline: `corepack pnpm check`

## Dependency Policy

- Resolve current official documentation through Context7 before adding or updating a library.
- Check official security advisories after Context7; Context7 does not replace vulnerability review.
- Pin exact stable versions. Do not introduce ranges, prereleases, or floating SDKs.
- Update lockfiles, SBOM inputs, documentation, and tests in the same change.

## Documentation Policy

- Update useful documentation in the same change as behavior.
- Do not describe planned behavior as implemented.
- Add an ADR for decisions that are expensive to reverse.
- Keep OpenAPI, IPC reference, runbooks, and commands executable and current.

## Validation Boundaries

- Browser and CI checks are automated evidence only.
- Windows, real touch, gloves, screen readers, and physical printers remain `not run` until performed on that environment.
- Kiosk compositions must have no page scroll and no touch target below the approved minimum.

## Secrets and Logs

- Never commit or print passwords, integration tokens, device credentials, activation codes, raw label payloads, or personal data.
- Logs must be structured, bounded, and redacted.

## Git Safety

- Preserve unrelated user changes and untracked local data.
- Do not use destructive reset or checkout commands without explicit approval.
- Stage only files owned by the current task and use focused commits.
```

- [ ] **Step 5: Rewrite the root README and create the documentation map**

Update `README.md` so it describes Puntiro as a shipment-label platform, retains the existing design-system commands, and links to `docs/README.md`.

Create `docs/README.md` with links grouped under:

```markdown
# Puntiro Documentation

## Product and UX
## Technical Architecture
## Architecture Decisions
## Runbooks
## Implementation Plans
## Validation Rule

Automated and physical acceptance are always reported separately.
```

Create `docs/adr/0001-modular-monolith-agent.md` recording status `Accepted`, context, decision, consequences, and rejected alternatives for:

- ASP.NET Core modular monolith;
- separate .NET Windows Agent;
- WPF/WebView2 React kiosk;
- PostgreSQL canonical data and SQLite local projection.

Create `docs/runbooks/development-bootstrap.md` with exact prerequisites and commands already valid in this repository:

```bash
node --version
corepack pnpm --version
dotnet --info
corepack pnpm install --frozen-lockfile
corepack pnpm docs:check
corepack pnpm check
```

- [ ] **Step 6: Add and run the documentation command**

Add to root `package.json` scripts:

```json
"docs:check": "node scripts/check-docs.mjs"
```

Run:

```bash
node --test scripts/check-docs.test.mjs
corepack pnpm docs:check
git diff --check
```

Expected: all commands PASS.

- [ ] **Step 7: Commit Task 1**

```bash
git add AGENTS.md README.md docs/README.md docs/adr/0001-modular-monolith-agent.md docs/runbooks/development-bootstrap.md scripts/check-docs.mjs scripts/check-docs.test.mjs package.json
git commit -m "docs: establish repository operating contract"
```

---

### Task 2: Exact Dependency and Toolchain Guardrails

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `scripts/check-dependency-policy.test.mjs`
- Create: `scripts/check-dependency-policy.mjs`
- Modify: `package.json`
- Modify: `AGENTS.md`
- Modify: `docs/runbooks/development-bootstrap.md`

**Interfaces:**
- Consumes: root rules and `docs:check` from Task 1.
- Produces: `node scripts/check-dependency-policy.mjs`, exact .NET SDK/compiler policy, central NuGet ownership.

- [ ] **Step 1: Revalidate versions before editing**

Use Context7 to resolve official documentation for `.NET` and `Entity Framework Core`. Record the query date and selected stable lines in the commit body. Verify Node and pnpm through their official release/security feeds. Then run:

Security revalidation note (2026-08-08): planned SDK `10.0.102` was rejected because Microsoft [CVE-2026-50646](https://github.com/dotnet/announcements/issues/418) affects Windows Desktop runtime `10.0.0` through `10.0.9`; the [.NET 10.0.10 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.10/10.0.10.md) list SDK `10.0.302` as carrying the patched runtime.

Node security revalidation note (2026-08-08): planned Node `24.18.0` was rejected because the official [July 29 security bulletin](https://nodejs.org/en/blog/vulnerability/july-2026-security-releases) fixes multiple high-severity 24.x issues in `24.18.1`; the official [distribution index](https://nodejs.org/dist/index.json) lists `24.19.0` (2026-08-03) as the current Krypton LTS.

```bash
dotnet --info
node --version
corepack pnpm --version
corepack pnpm audit --prod
```

Expected baseline pins for this plan: .NET SDK `10.0.302`, Node `24.19.0`, pnpm `11.17.0`, React `19.2.8`, Vite `8.1.5`. If an official security advisory rejects one of these exact versions, stop and amend the plan before implementation rather than silently substituting a version.

- [ ] **Step 2: Write the failing dependency-policy test**

Create `scripts/check-dependency-policy.test.mjs`:

```js
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateDependencyPolicy } from './check-dependency-policy.mjs';

test('toolchains and package manifests use exact stable versions', async () => {
  const errors = await validateDependencyPolicy(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});
```

Run:

```bash
node --test scripts/check-dependency-policy.test.mjs
```

Expected: FAIL with `ERR_MODULE_NOT_FOUND` for `check-dependency-policy.mjs`.

- [ ] **Step 3: Add exact .NET toolchain files**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "disable",
    "allowPrerelease": false
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Implement the exact-version checker**

Create `scripts/check-dependency-policy.mjs`:

```js
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
```

- [ ] **Step 5: Add the command and document it**

Add to root scripts:

```json
"dependencies:check": "node scripts/check-dependency-policy.mjs"
```

Add this real command to `AGENTS.md` and `docs/runbooks/development-bootstrap.md` in the same change:

```bash
corepack pnpm dependencies:check
```

- [ ] **Step 6: Verify GREEN**

Run:

```bash
node --test scripts/check-dependency-policy.test.mjs
corepack pnpm dependencies:check
corepack pnpm docs:check
git diff --check
```

Expected: PASS.

- [ ] **Step 7: Commit Task 2**

```bash
git add global.json Directory.Build.props Directory.Packages.props scripts/check-dependency-policy.mjs scripts/check-dependency-policy.test.mjs package.json AGENTS.md docs/runbooks/development-bootstrap.md
git commit -m "build: enforce exact dependency policy"
```

---

### Task 3: .NET Solution and Process Boundaries

**Files:**
- Create: `Puntiro.slnx`
- Create: `src/Puntiro.Contracts/Puntiro.Contracts.csproj`
- Create: `src/Puntiro.Contracts/ProtocolVersion.cs`
- Create: `apps/cloud/Puntiro.Cloud.csproj`
- Create: `apps/cloud/Program.cs`
- Create: `apps/agent/Puntiro.Agent.csproj`
- Create: `apps/agent/Program.cs`
- Create: `apps/kiosk-shell/Puntiro.KioskShell.csproj`
- Create: `apps/kiosk-shell/App.xaml`
- Create: `apps/kiosk-shell/App.xaml.cs`
- Create: `apps/kiosk-shell/MainWindow.xaml`
- Create: `apps/kiosk-shell/MainWindow.xaml.cs`
- Create: `scripts/check-foundation.test.mjs`
- Create: `scripts/check-foundation.mjs`
- Modify: `package.json`
- Modify: `AGENTS.md`
- Modify: `docs/runbooks/development-bootstrap.md`

**Interfaces:**
- Consumes: exact .NET policy from Task 2.
- Produces: `Puntiro.Contracts.ProtocolVersion.Current`, Cloud `/health/live`, buildable Cloud/Agent/KioskShell process boundaries, `foundation:check`.

- [ ] **Step 1: Write the failing foundation contract**

Create `scripts/check-foundation.test.mjs`:

```js
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateFoundation } from './check-foundation.mjs';

test('product process boundaries are present and one-way', async () => {
  const errors = await validateFoundation(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});
```

Run:

```bash
node --test scripts/check-foundation.test.mjs
```

Expected: FAIL because `check-foundation.mjs` does not exist.

- [ ] **Step 2: Create the solution and contracts boundary**

Create `Puntiro.slnx`:

```xml
<Solution>
  <Folder Name="/apps/">
    <Project Path="apps/agent/Puntiro.Agent.csproj" />
    <Project Path="apps/cloud/Puntiro.Cloud.csproj" />
    <Project Path="apps/kiosk-shell/Puntiro.KioskShell.csproj" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/Puntiro.Contracts/Puntiro.Contracts.csproj" />
  </Folder>
</Solution>
```

Create `src/Puntiro.Contracts/Puntiro.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk" />
```

Create `src/Puntiro.Contracts/ProtocolVersion.cs`:

```csharp
namespace Puntiro.Contracts;

public static class ProtocolVersion
{
    public const int Current = 1;
}
```

- [ ] **Step 3: Create the Cloud host boundary**

Create `apps/cloud/Puntiro.Cloud.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" />
  </ItemGroup>
</Project>
```

Create `apps/cloud/Program.cs`:

```csharp
using Puntiro.Contracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "alive",
    protocolVersion = ProtocolVersion.Current
}));

app.Run();

public partial class Program;
```

- [ ] **Step 4: Create the Agent process boundary**

Create `apps/agent/Puntiro.Agent.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" />
  </ItemGroup>
</Project>
```

Create `apps/agent/Program.cs`:

```csharp
using Puntiro.Contracts;

Console.WriteLine($"Puntiro Agent protocol {ProtocolVersion.Current}");
```

This task intentionally does not register a Windows Service or background worker; lifecycle belongs to the Windows Agent plan.

- [ ] **Step 5: Create the WPF shell boundary**

Create `apps/kiosk-shell/Puntiro.KioskShell.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" />
  </ItemGroup>
</Project>
```

Create `apps/kiosk-shell/App.xaml`:

```xml
<Application x:Class="Puntiro.KioskShell.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
</Application>
```

Create `apps/kiosk-shell/App.xaml.cs`:

```csharp
using System.Windows;

namespace Puntiro.KioskShell;

public partial class App : Application;
```

Create `apps/kiosk-shell/MainWindow.xaml`:

```xml
<Window x:Class="Puntiro.KioskShell.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Puntiro Kiosk Foundation"
        Width="1280"
        Height="800">
  <Grid>
    <TextBlock HorizontalAlignment="Center"
               VerticalAlignment="Center"
               FontSize="32"
               Text="Puntiro Kiosk Foundation" />
  </Grid>
</Window>
```

Create `apps/kiosk-shell/MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace Puntiro.KioskShell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

WebView2 is deliberately deferred until its package is Context7/security-reviewed in the Kiosk integration plan.

- [ ] **Step 6: Implement the foundation checker**

Create `scripts/check-foundation.mjs`:

```js
import { access, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const projects = [
  'src/Puntiro.Contracts/Puntiro.Contracts.csproj',
  'apps/cloud/Puntiro.Cloud.csproj',
  'apps/agent/Puntiro.Agent.csproj',
  'apps/kiosk-shell/Puntiro.KioskShell.csproj'
];

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
  const appProjects = projects.filter(relativePath => relativePath.startsWith('apps/'));

  for (const relativePath of appProjects) {
    if (!contents[relativePath].includes('Puntiro.Contracts.csproj')) {
      errors.push(`${relativePath} must reference Puntiro.Contracts`);
    }
  }
  if (contents[projects[0]].includes('ProjectReference')) {
    errors.push('Puntiro.Contracts must not reference an application');
  }
  if (!contents['apps/cloud/Puntiro.Cloud.csproj'].includes('Microsoft.NET.Sdk.Web')) {
    errors.push('Cloud must use Microsoft.NET.Sdk.Web');
  }
  for (const relativePath of projects.filter(item => item !== 'apps/cloud/Puntiro.Cloud.csproj')) {
    if (contents[relativePath].includes('Microsoft.NET.Sdk.Web')) {
      errors.push(`${relativePath} must not use Microsoft.NET.Sdk.Web`);
    }
  }
  if (!contents['apps/kiosk-shell/Puntiro.KioskShell.csproj'].includes('<UseWPF>true</UseWPF>')) {
    errors.push('Kiosk Shell must enable WPF');
  }

  const forbidden = ['Microsoft.Data.Sqlite', 'System.Net.Sockets', 'System.Printing', 'Microsoft.Web.WebView2'];
  for (const [relativePath, content] of Object.entries(contents)) {
    for (const marker of forbidden) {
      if (content.includes(marker)) errors.push(`${relativePath} contains deferred dependency ${marker}`);
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
```

Add root script:

```json
"foundation:check": "node scripts/check-foundation.mjs"
```

Add these now-valid commands to `AGENTS.md` and `docs/runbooks/development-bootstrap.md`:

```bash
corepack pnpm foundation:check
dotnet build Puntiro.slnx --configuration Release
```

- [ ] **Step 7: Restore, build and verify GREEN**

Run on macOS/Linux:

```bash
node --test scripts/check-foundation.test.mjs
corepack pnpm foundation:check
dotnet restore Puntiro.slnx
dotnet build Puntiro.slnx --configuration Release --no-restore
```

Run on Windows or GitHub Windows runner:

```powershell
dotnet restore Puntiro.slnx
dotnet build Puntiro.slnx --configuration Release --no-restore
```

Expected: all commands PASS. Record WPF runtime acceptance as `not run`; this task proves compilation only.

- [ ] **Step 8: Commit Task 3**

```bash
git add Puntiro.slnx src/Puntiro.Contracts apps/cloud apps/agent apps/kiosk-shell scripts/check-foundation.mjs scripts/check-foundation.test.mjs package.json AGENTS.md docs/runbooks/development-bootstrap.md
git commit -m "build: add product process boundaries"
```

---

### Task 4: React Admin and Kiosk Application Shells

**Files:**
- Create: `apps/admin/package.json`
- Create: `apps/admin/tsconfig.json`
- Create: `apps/admin/vite.config.ts`
- Create: `apps/admin/index.html`
- Create: `apps/admin/src/main.tsx`
- Create: `apps/admin/src/App.tsx`
- Create: `apps/kiosk-web/package.json`
- Create: `apps/kiosk-web/tsconfig.json`
- Create: `apps/kiosk-web/vite.config.ts`
- Create: `apps/kiosk-web/index.html`
- Create: `apps/kiosk-web/src/main.tsx`
- Create: `apps/kiosk-web/src/App.tsx`
- Create: `scripts/product-shells.test.mjs`
- Modify: `scripts/check-foundation.mjs`

**Interfaces:**
- Consumes: `@puntiro/ui` and root TypeScript policy.
- Produces: buildable `@puntiro/admin` and `@puntiro/kiosk-web` packages with no backend or printer access.

- [ ] **Step 1: Revalidate React and Vite versions**

Use Context7 to resolve official React and Vite documentation and verify compatibility with the existing TypeScript/UI toolchain. Verify their official security advisories. The exact expected pins are React/ReactDOM `19.2.8`, `@vitejs/plugin-react` `6.0.4`, and Vite `8.1.5`. If an advisory rejects a pin, stop and amend the plan before editing manifests.

- [ ] **Step 2: Write the failing shell contract**

Create `scripts/product-shells.test.mjs`:

```js
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

for (const app of ['admin', 'kiosk-web']) {
  test(`${app} is a private buildable UI package`, async () => {
    const pkg = JSON.parse(await readFile(new URL(`../apps/${app}/package.json`, import.meta.url), 'utf8'));
    assert.equal(pkg.private, true);
    assert.equal(pkg.dependencies['@puntiro/ui'], 'workspace:*');
    assert.equal(pkg.dependencies.react, '19.2.8');
    assert.equal(pkg.devDependencies.vite, '8.1.5');
  });
}
```

Run:

```bash
node --test scripts/product-shells.test.mjs
```

Expected: FAIL with `ENOENT` for `apps/admin/package.json`.

- [ ] **Step 3: Create exact package manifests**

Both applications use this shape, changing only package name:

```json
{
  "name": "@puntiro/admin",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "vite build",
    "typecheck": "tsc --project tsconfig.json"
  },
  "dependencies": {
    "@puntiro/ui": "workspace:*",
    "react": "19.2.8",
    "react-dom": "19.2.8"
  },
  "devDependencies": {
    "@types/react": "19.2.17",
    "@types/react-dom": "19.2.3",
    "@vitejs/plugin-react": "6.0.4",
    "vite": "8.1.5"
  }
}
```

For kiosk use `"name": "@puntiro/kiosk-web"`.

- [ ] **Step 4: Create focused TypeScript and Vite configuration**

Create this `tsconfig.json` in both applications:

```json
{
  "extends": "../../tsconfig.base.json",
  "include": ["src", "vite.config.ts"]
}
```

Each `vite.config.ts` uses:

```ts
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()]
});
```

- [ ] **Step 5: Create minimal shells using public UI only**

`apps/admin/src/App.tsx`:

```tsx
import { PuntiroProvider, Surface } from '@puntiro/ui';
import '@puntiro/ui/styles.css';

export function App() {
  return (
    <PuntiroProvider locale="ru" interactionMode="standard">
      <Surface>
        <main>
          <h1>Puntiro Admin</h1>
          <p>Инфраструктура административного приложения готова.</p>
        </main>
      </Surface>
    </PuntiroProvider>
  );
}
```

Create `apps/kiosk-web/src/App.tsx`:

```tsx
import { PuntiroProvider, Surface } from '@puntiro/ui';
import '@puntiro/ui/styles.css';

export function App() {
  return (
    <PuntiroProvider locale="ru" interactionMode="touch">
      <Surface>
        <main>
          <h1>Puntiro Kiosk</h1>
          <p>Инфраструктура киоска готова.</p>
        </main>
      </Surface>
    </PuntiroProvider>
  );
}
```

It must not import `fetch`, SQLite, Node modules or printer APIs.

Both `main.tsx` files mount `<App />` through `createRoot`. Both `index.html` files contain a single `#root` element and a module reference to `/src/main.tsx`.

Use this `main.tsx` in each application:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';

const root = document.getElementById('root');
if (!root) throw new Error('Application root is missing');

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>
);
```

Use this `index.html`, changing the title to `Puntiro Admin` or `Puntiro Kiosk`:

```html
<!doctype html>
<html lang="ru">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Puntiro Admin</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 6: Extend the foundation checker**

Before `return errors;` in `validateFoundation`, add:

```js
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
      if (app === 'kiosk-web' && !source.includes('interactionMode="touch"')) {
        errors.push('kiosk-web must use touch interaction mode');
      }
      for (const marker of forbiddenUiMarkers) {
        if (source.includes(marker)) errors.push(`${app} contains forbidden boundary marker ${marker}`);
      }
    } catch {
      errors.push(`Missing product shell files for ${app}`);
    }
  }
```

- [ ] **Step 7: Install and verify GREEN**

Run:

```bash
corepack pnpm install --lockfile-only
CI=true corepack pnpm install --frozen-lockfile
node --test scripts/product-shells.test.mjs scripts/check-foundation.test.mjs
corepack pnpm --filter @puntiro/ui build
corepack pnpm --filter @puntiro/admin typecheck
corepack pnpm --filter @puntiro/kiosk-web typecheck
corepack pnpm --filter @puntiro/admin build
corepack pnpm --filter @puntiro/kiosk-web build
corepack pnpm dependencies:check
corepack pnpm foundation:check
git diff --check
```

Expected: all commands PASS. No product screen or Cloud connection is claimed.

- [ ] **Step 8: Commit Task 4**

```bash
git add apps/admin apps/kiosk-web scripts/product-shells.test.mjs scripts/check-foundation.mjs pnpm-lock.yaml
git commit -m "build: add React product shells"
```

---

### Task 5: Foundation CI and Final Verification

**Files:**
- Create: `.github/workflows/foundation.yml`
- Create: `scripts/ci-contract.test.mjs`
- Modify: `package.json`
- Modify: `AGENTS.md`
- Modify: `docs/runbooks/development-bootstrap.md`
- Create: `docs/engineering/foundation-validation.md`

**Interfaces:**
- Consumes: Tasks 1–4 commands and buildable boundaries.
- Produces: `corepack pnpm check:foundation`, Linux contract job, Windows .NET/WPF build job, explicit validation record.

- [ ] **Step 1: Write the failing CI contract**

Create `scripts/ci-contract.test.mjs`:

```js
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

test('foundation CI separates repository and Windows compilation evidence', async () => {
  const workflow = await readFile(new URL('../.github/workflows/foundation.yml', import.meta.url), 'utf8');
  assert.match(workflow, /name: Repository contracts/);
  assert.match(workflow, /name: Windows compile/);
  assert.match(workflow, /node-version: 24\.19\.0/);
  assert.match(workflow, /dotnet-version: 10\.0\.302/);
  assert.match(workflow, /corepack pnpm install --frozen-lockfile/);
  assert.doesNotMatch(workflow, /Windows acceptance/);
});
```

Run:

```bash
node --test scripts/ci-contract.test.mjs
```

Expected: FAIL with `ENOENT` for `.github/workflows/foundation.yml`.

- [ ] **Step 2: Create the foundation workflow**

Create `.github/workflows/foundation.yml`:

```yaml
name: Repository foundation

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

concurrency:
  group: foundation-${{ github.ref }}
  cancel-in-progress: true

jobs:
  repository-contracts:
    name: Repository contracts
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4.2.2
      - uses: actions/setup-node@v4.4.0
        with:
          node-version: 24.19.0
      - uses: actions/setup-dotnet@v5.0.0
        with:
          dotnet-version: 10.0.302
      - run: corepack enable
      - run: corepack prepare pnpm@11.17.0 --activate
      - run: corepack pnpm install --frozen-lockfile
      - run: corepack pnpm check:foundation
      - run: dotnet restore Puntiro.slnx
      - run: dotnet build Puntiro.slnx --configuration Release --no-restore

  windows-build:
    name: Windows compile
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4.2.2
      - uses: actions/setup-dotnet@v5.0.0
        with:
          dotnet-version: 10.0.302
      - run: dotnet restore Puntiro.slnx
      - run: dotnet build Puntiro.slnx --configuration Release --no-restore
```

Before committing this workflow, security-review the exact action release tags `v4.2.2`, `v4.4.0`, and `v5.0.0`. If an official advisory rejects one, stop and amend the plan before changing the pin.

Do not put secrets in the workflow. Do not run physical or Windows runtime acceptance in the Ubuntu job.

- [ ] **Step 3: Add the aggregate command**

Add to root scripts:

```json
"test:repository": "node --test scripts/check-docs.test.mjs scripts/check-dependency-policy.test.mjs scripts/check-foundation.test.mjs scripts/product-shells.test.mjs scripts/ci-contract.test.mjs",
"check:dotnet": "dotnet build Puntiro.slnx --configuration Release",
"dependencies:audit": "corepack pnpm audit --prod --audit-level high && dotnet package list Puntiro.slnx --vulnerable --include-transitive",
"check:foundation": "corepack pnpm docs:check && corepack pnpm dependencies:check && corepack pnpm dependencies:audit && corepack pnpm test:repository && corepack pnpm foundation:check && corepack pnpm --filter @puntiro/ui build && corepack pnpm --filter @puntiro/admin typecheck && corepack pnpm --filter @puntiro/kiosk-web typecheck && corepack pnpm --filter @puntiro/admin build && corepack pnpm --filter @puntiro/kiosk-web build && corepack pnpm check:dotnet"
```

- [ ] **Step 4: Verify the CI contract GREEN**

Run:

```bash
node --test scripts/ci-contract.test.mjs
corepack pnpm test:repository
corepack pnpm check:foundation
```

Expected: PASS.

- [ ] **Step 5: Review the Windows compilation job**

Confirm the YAML already contains the `windows-build` job using `windows-latest` and .NET `10.0.302`. It runs:

```powershell
dotnet restore Puntiro.slnx
dotnet build Puntiro.slnx --configuration Release --no-restore
```

The job name must say `Windows compile`, not `Windows acceptance`.

- [ ] **Step 6: Align operational documentation**

Update `AGENTS.md` and the bootstrap runbook so the aggregate command is exactly:

```bash
corepack pnpm check:foundation
```

Also document the independently runnable vulnerability gate:

```bash
corepack pnpm dependencies:audit
```

Create `docs/engineering/foundation-validation.md`:

```markdown
# Repository Foundation Validation

## Automated

- Documentation contracts: pass
- Dependency policy: pass
- Repository contracts: pass
- Admin shell typecheck/build: pass
- Kiosk shell typecheck/build: pass
- .NET solution build on development host: pass
- Windows compile: pending CI until the workflow runs

## Manual

- WPF runtime on Windows: not run
- Windows Service installation: not run
- Touch and gloves: not run
- Screen reader: not run
- Physical printers: not run

Automated evidence does not upgrade any manual status.
```

If the Windows workflow has already completed successfully before commit, replace `pending CI` with `pass` and link the run. Otherwise retain `pending CI`; never infer it locally.

- [ ] **Step 7: Run the complete foundation gate**

Run:

```bash
corepack pnpm check:foundation
corepack pnpm docs:check
corepack pnpm dependencies:check
git diff --check
git status --short
```

Expected:

- every automated local command PASS;
- only intended Task 5 files are modified;
- `.pnpm-store/` remains untracked and unstaged;
- physical gates remain `not run`.

- [ ] **Step 8: Commit Task 5**

```bash
git add .github/workflows/foundation.yml scripts/ci-contract.test.mjs package.json AGENTS.md docs/runbooks/development-bootstrap.md docs/engineering/foundation-validation.md
git commit -m "ci: verify repository foundation"
```

---

## Plan Self-Review Checklist

- Every architecture-wide engineering rule has an owning Task 1, 2 or 5 artifact.
- Every created process boundary has an exact path and one-way dependency contract.
- Product shells use only public `@puntiro/ui` exports and contain no business integration.
- Context7/security review happens before dependency edits.
- No task claims Windows or hardware acceptance from compilation.
- Every task contains RED, GREEN, focused verification and a commit.
- No domain API, printer, sync or persistence implementation leaks into foundation.
