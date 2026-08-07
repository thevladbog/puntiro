# Puntiro Kiosk Core Flow Compositions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the approved Puntiro kiosk happy path as reusable presentational patterns and fully testable Storybook compositions, from shipment queue and partial-number search through direct print launch, progress, and success.

**Architecture:** Extend `@puntiro/ui` with closed, controlled APIs that receive prepared domain data and callbacks but perform no routing, persistence, HTTP, Tauri, timers, or hardware I/O. Storybook owns the interactive demo harness that connects these patterns into a state machine, simulates application callbacks, and models the three-second success countdown without placing timer ownership in the UI package. Existing components, semantic tokens, RU/EN provider, browser tests, and Playwright visual contracts remain the foundation.

**Tech Stack:** React 19.2.8, TypeScript 6.0.3, React Aria Components 1.19.0, CSS Modules, DTCG tokens with Style Dictionary 5.5.0, Storybook 10.5.3, Vitest 4.1.10 browser mode, Playwright 1.61.1, pnpm 11.17.0.

## Global Constraints

- Reference kiosk viewport is `1280 x 800`, landscape, adapting only upward.
- Kiosk compositions have no page scroll or nested working-region scroll.
- Queue capacity is exactly 6 cards at `1280 x 800`, 9 at `1600 x 900`, and 12 at `1920 x 1080`.
- Touch targets are at least 64 px; comfortable primary actions are at least 72 px.
- The visual direction is Operational Calm using existing Puntiro tokens only.
- Onest is the interface font; IBM Plex Mono is used for shipment numbers, counters, and technical values.
- RU is the default locale; every new composition also has EN copy and long-content coverage.
- Shipment cards show shipment number, consignee name, planned shipment date, and short status; product contents are never shown.
- Search is case-insensitive substring matching, numeric-first, and supports Cyrillic, Latin, and symbol layers without a physical keyboard.
- There is no separate review screen before printing.
- For one ready printer, the place-count primary action launches printing directly; for multiple ready printers it opens printer choice.
- Place counts are integers from 1 to 100; values above 10 require an in-context confirmation dialog.
- Printing has no back, navigation, retry, or duplicate-submit action.
- Unknown print result never triggers automatic retry or success.
- New library APIs must not expose `className`, `style`, React Aria types, Lucide types, routing, HTTP, Tauri, or printer drivers.
- Production timer ownership, persistence, API synchronization, Tauri integration, and physical printing remain outside this plan; only the Storybook harness models the approved countdown.
- All commands use Node 24 and `corepack pnpm` with pinned pnpm 11.17.0.
- Windows, physical touch, gloves, NVDA, ZPL hardware, and TSPL hardware remain manual acceptance gates and must not be reported as automated passes.

---

## File and Responsibility Map

### Token foundation

- `packages/tokens/src/semantic/interaction.tokens.json`: expose the approved 72 px comfortable-control role.
- `packages/tokens/src/component/core.tokens.json`: bind comfortable primary-action sizing to `Button` and new kiosk patterns.
- `packages/tokens/src/token-contract.test.ts`: verify source aliases and generated CSS/JSON/TypeScript artifacts.
- `packages/tokens/dist/tokens.css`, `tokens.json`, `tokens.ts`: generated outputs committed by the existing build.

### Public UI patterns

- `packages/ui/src/patterns/shipment.types.ts`: one canonical `ShipmentTaskSummary` shared by queue, search, and steps.
- `packages/ui/src/patterns/ShipmentTaskCard/*`: card contract with consignee, planned date, short status, and accessible full name.
- `packages/ui/src/patterns/KioskShell/*`: fixed viewport shell, visible Puntiro identity, health state, and optional section navigation.
- `packages/ui/src/patterns/ShipmentQueue/*`: controlled pagination and responsive 6/9/12 grid.
- `packages/ui/src/patterns/ShipmentSearch/*`: controlled partial search, result pagination, and on-screen keyboard layers.
- `packages/ui/src/patterns/PlaceCountStep/*`: place count, numeric keypad, direct-print or choose-printer action, and over-10 dialog.
- `packages/ui/src/patterns/PrinterSelectionStep/*`: paginated printer choice and direct print action.
- `packages/ui/src/patterns/PrintingStep/*`: locked full-screen wrapper around `PrintProgress`.
- `packages/ui/src/patterns/PrintSuccess/*`: success summary and explicit early return.
- `packages/ui/src/index.ts`: public exports and types.

### Storybook compositions

- `apps/storybook/src/stories/Compositions/fixtures.ts`: deterministic RU/EN-ready shipments, printers, long values, and dates.
- `apps/storybook/src/stories/Compositions/*.stories.tsx`: fixed screen stories plus an interactive happy-path harness.
- `apps/storybook/src/docs/compositions/CoreFlow.mdx`: localized flow overview.
- `apps/storybook/src/docs/content.ru.ts`, `content.en.ts`, `articles.test.ts`: replace the future-only text with approved composition documentation.
- `apps/storybook/src/docs/maturity-manifest.ts`, `maturity-manifest.test.ts`: register new Beta exports since `0.2.0` with manual gates false.

### Verification

- `apps/storybook/tests/helpers.ts`: viewport-aware opening and no-scroll/target helpers.
- `apps/storybook/tests/visual-cases.ts`, `visual.spec.ts`: RU/EN happy-path baselines and responsive queue cases.
- `apps/storybook/tests/kiosk-core-flow.spec.ts`: adaptive capacity, search, direct print routing, large-count confirmation, locked printing, and success behavior.
- `apps/storybook/tests/visual.spec.ts-snapshots/*`: generated baseline PNGs.

---

### Task 1: Add the Comfortable Primary-Action Token Contract

**Files:**
- Modify: `packages/tokens/src/semantic/interaction.tokens.json`
- Modify: `packages/tokens/src/component/core.tokens.json`
- Modify: `packages/tokens/src/token-contract.test.ts`
- Regenerate: `packages/tokens/dist/tokens.css`
- Regenerate: `packages/tokens/dist/tokens.json`
- Regenerate: `packages/tokens/dist/tokens.ts`
- Modify: `packages/ui/src/components/Button/Button.module.css`
- Modify: `packages/ui/src/components/Button/Button.test.tsx`

**Interfaces:**
- Produces CSS variable `--puntiro-semantic-size-control-comfortable` resolving to 72 px.
- Produces CSS variable `--puntiro-component-button-primary-min-block-size` aliasing the comfortable role.
- Primary `Button` consumes the component alias and removes its existing direct reference-token dependency; later patterns receive 72 px sizing through Button rather than reading a reference token.

- [ ] **Step 1: Write the failing token contract**

Add these assertions to `packages/tokens/src/token-contract.test.ts`:

```ts
const semanticAliasContracts = [
  // existing entries
  ['interaction', 'semantic.size.control.comfortable', '{reference.size.touch.comfortable}'],
] as const;

it('publishes the comfortable primary-action aliases', () => {
  const css = readText('../dist/tokens.css');
  const json = readJson('../dist/tokens.json');

  expect(css).toContain(
    '--puntiro-semantic-size-control-comfortable: var(--puntiro-reference-size-touch-comfortable);',
  );
  expect(css).toContain(
    '--puntiro-component-button-primary-min-block-size: var(--puntiro-semantic-size-control-comfortable);',
  );
  expect(json).toHaveProperty('semantic.size.control.comfortable', { value: 72, unit: 'px' });
});
```

- [ ] **Step 2: Run the focused test and confirm RED**

Run:

```bash
corepack pnpm exec vitest run packages/tokens/src/token-contract.test.ts
```

Expected: FAIL because `semantic.size.control.comfortable` and the Button alias do not exist.

- [ ] **Step 3: Add the DTCG aliases and regenerate outputs**

Add to `semantic.size.control` in `interaction.tokens.json`:

```json
"comfortable": {
  "$type": "dimension",
  "$value": "{reference.size.touch.comfortable}",
  "$description": "Comfortable primary action block size in touch mode."
}
```

Add to `component.Button` in `core.tokens.json`:

```json
"primaryMinBlockSize": {
  "$type": "dimension",
  "$value": "{semantic.size.control.comfortable}",
  "$description": "Comfortable touch size for the primary action."
}
```

Run:

```bash
corepack pnpm tokens:build
```

Replace the touch primary rule in `Button.module.css`:

```css
:global([data-interaction-mode='touch']) .root[data-primary-action='true'] {
  min-block-size: var(--puntiro-component-button-primary-min-block-size);
}
```

Add a source contract to `Button.test.tsx`:

```ts
import { readFileSync } from 'node:fs';

it('uses the comfortable component token without a reference-token leak', () => {
  const css = readFileSync(new URL('./Button.module.css', import.meta.url), 'utf8');
  expect(css).toContain('--puntiro-component-button-primary-min-block-size');
  expect(css).not.toContain('--puntiro-reference-');
});
```

- [ ] **Step 4: Verify GREEN and reproducibility**

Run:

```bash
corepack pnpm exec vitest run packages/tokens/src/token-contract.test.ts
corepack pnpm exec vitest run packages/ui/src/components/Button/Button.test.tsx
corepack pnpm tokens:check
```

Expected: token test PASS and generated-artifact check exit 0.

- [ ] **Step 5: Commit**

```bash
git add packages/tokens/src packages/tokens/dist packages/ui/src/components/Button
git commit -m "feat: add comfortable kiosk action tokens"
```

---

### Task 2: Refine ShipmentTaskCard for Queue Recognition

**Files:**
- Create: `packages/ui/src/patterns/shipment.types.ts`
- Modify: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.types.ts`
- Modify: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.tsx`
- Modify: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.module.css`
- Modify: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.test.tsx`
- Modify: `packages/ui/src/index.ts`
- Modify: `apps/storybook/src/stories/fixtures.ts`
- Modify: `apps/storybook/src/stories/KioskPatterns/ShipmentTaskCard.stories.tsx`
- Modify: `apps/storybook/src/stories/KioskPatterns/shipmentTaskCard.docs.ts`
- Modify: `apps/storybook/src/stories/KioskPatterns/shipmentTaskCard.contract.test.ts`

**Interfaces:**
- Produces:

```ts
export type ShipmentTaskStatus = 'ready' | 'updated' | 'attention';

export interface ShipmentTaskSummary {
  id: string;
  shipmentNumber: string;
  consigneeName: string;
  plannedShipAt: Date;
  status: ShipmentTaskStatus;
}

export interface ShipmentTaskCardProps extends Omit<ShipmentTaskSummary, 'id'> {
  onOpen: () => void;
  isSelected?: boolean;
}
```

- Removes the required `receivedAt` display contract from the Beta card.
- Queue and search tasks consume `ShipmentTaskSummary` from this task.

- [ ] **Step 1: Write failing API and rendering tests**

Update `ShipmentTaskCard.test.tsx` with:

```tsx
it('renders the recognition hierarchy and full accessible name', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider locale="ru">
      <ShipmentTaskCard
        shipmentNumber="РН-842711"
        consigneeName="ООО Северный распределительный центр"
        plannedShipAt={new Date('2026-08-08T09:00:00.000Z')}
        status="ready"
        onOpen={() => undefined}
      />
    </PuntiroProvider>,
  );

  expect(html).toContain('РН-842711');
  expect(html).toContain('ООО Северный распределительный центр');
  expect(html).toContain('Плановая отгрузка');
  expect(html).toContain('Готово');
  expect(html).not.toContain('Получено');
  expect(html).toContain('aria-label="Отгрузка РН-842711, грузополучатель ООО Северный распределительный центр');
});
```

Add an exact-type assertion in `shipmentTaskCard.contract.test.ts`:

```ts
const propsAreExact: Equal<ShipmentTaskCardProps, {
  shipmentNumber: string;
  consigneeName: string;
  plannedShipAt: Date;
  status: ShipmentTaskStatus;
  onOpen: () => void;
  isSelected?: boolean;
}> = true;
void propsAreExact;
```

- [ ] **Step 2: Run focused tests and confirm RED**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.test.tsx apps/storybook/src/stories/KioskPatterns/shipmentTaskCard.contract.test.ts
```

Expected: FAIL because `consigneeName` is absent and `receivedAt` is still required.

- [ ] **Step 3: Implement the closed card contract**

Create `shipment.types.ts` with the interfaces above. Re-export the shared types from `ShipmentTaskCard.types.ts` and `packages/ui/src/index.ts`.

Render this fixed hierarchy inside the existing native button:

```tsx
const accessibleLabel = [
  copy.shipment,
  shipmentNumber,
  copy.consignee,
  consigneeName,
  copy.planned,
  formatTimestamp(plannedShipAt, locale),
].join(' ');

return <button
  type="button"
  aria-label={accessibleLabel}
  aria-describedby={statusId}
  className={className}
  data-kiosk-working-region="true"
  onClick={onOpen}
>
  <span className={styles.number}>{shipmentNumber}</span>
  <span className={styles.consignee}>{consigneeName}</span>
  <span className={styles.footer}>
    <span>{copy.planned} {formatTimestamp(plannedShipAt, locale)}</span>
    <span id={statusId}><StatusBadgeVisual tone={statusTone[status]} label={copy.statuses[status]} /></span>
  </span>
</button>;
```

Use short RU/EN statuses: `Готово / Изменено / Требует внимания` and `Ready / Updated / Needs attention`.

CSS must include:

```css
.consignee {
  display: -webkit-box;
  min-inline-size: 0;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  font-size: 1.125rem;
  line-height: 1.35;
}

.footer {
  align-items: end;
  display: flex;
  gap: var(--puntiro-semantic-space-compact);
  justify-content: space-between;
}
```

- [ ] **Step 4: Add long-consignee browser coverage**

Add `LongConsignee` to the existing story and assert:

```tsx
export const LongConsignee: Story = {
  args: { consigneeName: longConsigneeName },
  play: async ({ canvasElement }) => {
    const card = within(canvasElement).getByRole('button', { name: new RegExp(longConsigneeName) });
    const consignee = within(canvasElement).getByText(longConsigneeName);
    await expect(card).toHaveAccessibleName(new RegExp(longConsigneeName));
    await expect(consignee.scrollHeight).toBeLessThanOrEqual(consignee.clientHeight);
  },
};
```

Update docs anatomy and content rules to describe consignee, planned date, and the intentional removal of received time.

- [ ] **Step 5: Verify focused and browser GREEN**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.test.tsx apps/storybook/src/stories/KioskPatterns/shipmentTaskCard.contract.test.ts
CI=true corepack pnpm test:storybook -- --grep ShipmentTaskCard
corepack pnpm --filter @puntiro/ui build
```

Expected: focused unit/contract PASS, all ShipmentTaskCard stories PASS in Chromium, declaration boundary PASS.

- [ ] **Step 6: Commit**

```bash
git add packages/ui/src apps/storybook/src/stories
git commit -m "feat: refine shipment recognition card"
```

---

### Task 3: Build KioskShell and the Responsive Shipment Queue

**Files:**
- Create: `packages/ui/src/patterns/KioskShell/KioskShell.types.ts`
- Create: `packages/ui/src/patterns/KioskShell/KioskShell.tsx`
- Create: `packages/ui/src/patterns/KioskShell/KioskShell.module.css`
- Create: `packages/ui/src/patterns/KioskShell/KioskShell.test.tsx`
- Create: `packages/ui/src/patterns/ShipmentQueue/ShipmentQueue.types.ts`
- Create: `packages/ui/src/patterns/ShipmentQueue/ShipmentQueue.tsx`
- Create: `packages/ui/src/patterns/ShipmentQueue/ShipmentQueue.module.css`
- Create: `packages/ui/src/patterns/ShipmentQueue/ShipmentQueue.test.tsx`
- Create: `packages/ui/src/patterns/ShipmentQueue/kioskCapacity.ts`
- Create: `packages/ui/src/patterns/ShipmentQueue/kioskCapacity.test.ts`
- Create: `packages/ui/src/patterns/ShipmentQueue/useKioskCardCapacity.ts`
- Create: `apps/storybook/src/stories/Compositions/fixtures.ts`
- Create: `apps/storybook/src/stories/Compositions/ShipmentQueue.stories.tsx`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**

```ts
import type { ReactNode } from 'react';

export type KioskSection = 'shipments' | 'archive';
export type KioskConnectionState = 'online' | 'offline';

export interface KioskShellProps {
  connectionState: KioskConnectionState;
  printerSummary: string;
  activeSection: KioskSection;
  showNavigation?: boolean;
  onSectionChange: (section: KioskSection) => void;
  children: ReactNode;
}

export interface ShipmentQueueProps {
  tasks: readonly ShipmentTaskSummary[];
  page: number;
  anchorShipmentId?: string;
  onPageChange: (page: number) => void;
  onOpenShipment: (shipmentId: string) => void;
  onOpenSearch: () => void;
}

export function kioskCardCapacity(width: number, height: number): 6 | 9 | 12;
```

`ShipmentQueue` reads the actual kiosk window through a private resize hook and uses the pure `kioskCardCapacity` function. Server rendering defaults to 6. The public API stays deterministic and does not ask product callers to duplicate breakpoint logic.

- [ ] **Step 1: Write failing capacity and shell tests**

```ts
it.each([
  [1280, 800, 6],
  [1599, 899, 6],
  [1600, 900, 9],
  [1919, 1079, 9],
  [1920, 1080, 12],
] as const)('maps %ix%i to %i cards', (width, height, expected) => {
  expect(kioskCardCapacity(width, height)).toBe(expected);
});
```

```tsx
it('renders visible brand health and top-level navigation', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider>
      <KioskShell
        connectionState="online"
        printerSummary="Zebra ZD421 готов"
        activeSection="shipments"
        onSectionChange={() => undefined}
      >
        <main>Queue</main>
      </KioskShell>
    </PuntiroProvider>,
  );
  expect(html).toContain('Puntiro');
  expect(html).toContain('Онлайн');
  expect(html).toContain('Zebra ZD421 готов');
  expect(html).toContain('Отгрузки');
  expect(html).toContain('Архив');
});
```

- [ ] **Step 2: Confirm RED**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/ShipmentQueue/kioskCapacity.test.ts packages/ui/src/patterns/KioskShell/KioskShell.test.tsx
```

Expected: FAIL because both modules are absent.

- [ ] **Step 3: Implement capacity, shell, and queue pagination**

Capacity implementation:

```ts
export function kioskCardCapacity(width: number, height: number): 6 | 9 | 12 {
  if (width >= 1920 && height >= 1080) return 12;
  if (width >= 1600 && height >= 900) return 9;
  return 6;
}
```

Private resize hook:

```ts
function readCapacity(): 6 | 9 | 12 {
  if (typeof window === 'undefined') return 6;
  return kioskCardCapacity(window.innerWidth, window.innerHeight);
}

export function useKioskCardCapacity(): 6 | 9 | 12 {
  const [capacity, setCapacity] = useState<6 | 9 | 12>(readCapacity);

  useEffect(() => {
    const update = () => setCapacity(readCapacity());
    window.addEventListener('resize', update);
    update();
    return () => window.removeEventListener('resize', update);
  }, []);

  return capacity;
}
```

Queue page normalization:

```ts
const capacity = useKioskCardCapacity();
const pageCount = Math.max(1, Math.ceil(tasks.length / capacity));
const normalizedPage = Math.min(Math.max(page, 0), pageCount - 1);
const visibleTasks = tasks.slice(normalizedPage * capacity, (normalizedPage + 1) * capacity);
```

Preserve the last opened shipment across capacity changes and normalize deleted final pages:

```ts
const previousCapacity = useRef(capacity);

useEffect(() => {
  const anchorIndex = anchorShipmentId
    ? tasks.findIndex(({ id }) => id === anchorShipmentId)
    : -1;
  const fallbackIndex = page * previousCapacity.current;
  const desiredPage = Math.floor(Math.max(0, anchorIndex >= 0 ? anchorIndex : fallbackIndex) / capacity);
  const boundedPage = Math.min(desiredPage, pageCount - 1);

  previousCapacity.current = capacity;
  if (boundedPage !== page) onPageChange(boundedPage);
}, [anchorShipmentId, capacity, onPageChange, page, pageCount, tasks]);
```

Render the cards with stable IDs and explicit pager buttons. Wrap each card in an element with `data-queue-card={task.id}`. Set these root attributes for browser contracts:

```tsx
<section
  className={styles.root}
  data-kiosk-working-region="true"
  data-queue-capacity={capacity}
  aria-labelledby={titleId}
>
```

KioskShell uses the canonical inline P-Sequence geometry, visible `Puntiro` text, `data-puntiro-logo="true"`, and no dark-theme variant. The decorative SVG is `aria-hidden="true"`; the adjacent visible wordmark supplies the accessible brand text. Its root uses `block-size: 100dvh; overflow: hidden` and a three-row grid for health, content, and optional navigation.

```tsx
<span className={styles.brand} aria-label="Puntiro">
  <svg data-puntiro-logo="true" aria-hidden="true" viewBox="0 0 64 64">
    <g className={styles.markInk}>
      <rect x="6" y="6" width="10" height="52" rx="1" />
      <rect x="20" y="6" width="14" height="10" rx="1" />
      <rect x="38" y="6" width="14" height="10" rx="1" />
      <rect x="48" y="20" width="10" height="10" rx="1" />
      <rect x="38" y="34" width="14" height="10" rx="1" />
      <rect x="20" y="34" width="14" height="10" rx="1" />
    </g>
    <rect className={styles.markSignal} x="48" y="48" width="10" height="10" rx="1" />
  </svg>
  <span>Puntiro</span>
</span>
```

`markInk` uses semantic primary text and `markSignal` uses semantic primary action; the SVG never relies on inherited dark-manager colors.

Queue CSS uses explicit grid modifiers:

```css
.grid { display: grid; min-block-size: 0; gap: var(--puntiro-semantic-space-compact); }
.grid[data-capacity='6'] { grid-template-columns: repeat(2, minmax(0, 1fr)); grid-template-rows: repeat(3, minmax(0, 1fr)); }
.grid[data-capacity='9'] { grid-template-columns: repeat(3, minmax(0, 1fr)); grid-template-rows: repeat(3, minmax(0, 1fr)); }
.grid[data-capacity='12'] { grid-template-columns: repeat(4, minmax(0, 1fr)); grid-template-rows: repeat(3, minmax(0, 1fr)); }
```

Create deterministic Storybook shipments in `Compositions/fixtures.ts`:

```ts
export const shipmentTasks: readonly ShipmentTaskSummary[] = Array.from({ length: 13 }, (_, index) => ({
  id: `shipment-${index}`,
  shipmentNumber: index === 0 ? 'РН-842711' : `РН-${842710 - index}`,
  consigneeName: index === 0 ? 'ООО Северный распределительный центр' : `Грузополучатель ${index + 1}`,
  plannedShipAt: new Date(`2026-08-08T${String(9 + (index % 8)).padStart(2, '0')}:00:00Z`),
  status: index === 3 ? 'updated' : 'ready',
}));
```

- [ ] **Step 4: Add SSR and Chromium queue behavior tests**

Test SSR capacity and hidden navigation without a DOM test dependency:

```tsx
it('renders six cards by default and can hide section navigation', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><KioskShell
      connectionState="online"
      printerSummary="Zebra ZD421 готов"
      activeSection="shipments"
      showNavigation={false}
      onSectionChange={() => undefined}
    ><ShipmentQueue
      tasks={shipmentTasks}
      page={0}
      onPageChange={() => undefined}
      onOpenShipment={() => undefined}
      onOpenSearch={() => undefined}
    /></KioskShell></PuntiroProvider>,
  );
  expect(html.match(/data-queue-card=/gu)).toHaveLength(6);
  expect(html).toContain('Zebra ZD421 готов');
  expect(html).not.toContain('>Архив<');
});
```

Create `ShipmentQueue.stories.tsx` with a controlled render and this play:

```tsx
export const Default: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getAllByRole('button', { name: /Отгрузка/ })).toHaveLength(6);
    await userEvent.click(canvas.getByRole('button', { name: 'Следующая страница' }));
    await expect(args.onPageChange).toHaveBeenCalledWith(1);
    await userEvent.click(canvas.getByRole('button', { name: /РН-842711/ }));
    await expect(args.onOpenShipment).toHaveBeenCalledWith('shipment-0');
  },
};
```

- [ ] **Step 5: Verify and commit**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/KioskShell packages/ui/src/patterns/ShipmentQueue
CI=true corepack pnpm test:storybook -- --grep ShipmentQueue
corepack pnpm --filter @puntiro/ui typecheck
corepack pnpm --filter @puntiro/ui build
git add packages/ui/src apps/storybook/src/stories/Compositions/fixtures.ts apps/storybook/src/stories/Compositions/ShipmentQueue.stories.tsx
git commit -m "feat: add kiosk shell and adaptive queue"
```

Expected: unit tests, typecheck, UI build, and declaration boundary PASS.

---

### Task 4: Build Partial Shipment Search and the On-Screen Keyboard

**Files:**
- Create: `packages/ui/src/patterns/ShipmentSearch/ShipmentSearch.types.ts`
- Create: `packages/ui/src/patterns/ShipmentSearch/ShipmentSearch.tsx`
- Create: `packages/ui/src/patterns/ShipmentSearch/ShipmentSearch.module.css`
- Create: `packages/ui/src/patterns/ShipmentSearch/ShipmentSearch.test.tsx`
- Create: `packages/ui/src/patterns/ShipmentSearch/searchShipments.ts`
- Create: `packages/ui/src/patterns/ShipmentSearch/searchShipments.test.ts`
- Create: `packages/ui/src/patterns/ShipmentSearch/keyboardLayouts.ts`
- Create: `packages/ui/src/patterns/ShipmentSearch/keyboardLayouts.test.ts`
- Create: `apps/storybook/src/stories/Compositions/ShipmentSearch.stories.tsx`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**

```ts
export type SearchKeyboardLayer = 'numeric' | 'cyrillic' | 'latin' | 'symbols';

export interface ShipmentSearchProps {
  tasks: readonly ShipmentTaskSummary[];
  query: string;
  layer: SearchKeyboardLayer;
  page: number;
  resultsPerPage: number;
  onQueryChange: (query: string) => void;
  onLayerChange: (layer: SearchKeyboardLayer) => void;
  onPageChange: (page: number) => void;
  onOpenShipment: (shipmentId: string) => void;
  onBack: () => void;
}

export function searchShipments(
  tasks: readonly ShipmentTaskSummary[],
  query: string,
  locale: 'ru' | 'en',
): readonly ShipmentTaskSummary[];
```

- [ ] **Step 1: Write RED tests for substring search and keyboard coverage**

```ts
const tasks: readonly ShipmentTaskSummary[] = [
  { id: 'a', shipmentNumber: 'РН-842711', consigneeName: 'ООО Север', plannedShipAt: new Date('2026-08-08T09:00:00Z'), status: 'ready' },
  { id: 'b', shipmentNumber: 'AB-100', consigneeName: 'West Hub', plannedShipAt: new Date('2026-08-08T10:00:00Z'), status: 'ready' },
  { id: 'c', shipmentNumber: 'РН-842694', consigneeName: 'ООО Юг', plannedShipAt: new Date('2026-08-08T11:00:00Z'), status: 'updated' },
];

it('matches any case-insensitive part without reordering the queue', () => {
  expect(searchShipments(tasks, '842', 'ru').map(({ id }) => id)).toEqual(['a', 'c']);
  expect(searchShipments(tasks, 'ab-', 'en').map(({ id }) => id)).toEqual(['b']);
});

it('does not mirror the whole queue for an empty query', () => {
  expect(searchShipments(tasks, '   ', 'ru')).toEqual([]);
});

it('covers both alphabets and the approved separators', () => {
  expect(KEYBOARD_LAYOUTS.cyrillic.join('')).toContain('Я');
  expect(KEYBOARD_LAYOUTS.latin.join('')).toContain('Z');
  for (const symbol of ['-', '_', '/', '.', ':', '#', '+', '(', ')', ' ']) {
    expect(KEYBOARD_LAYOUTS.symbols).toContain(symbol);
  }
});
```

- [ ] **Step 2: Confirm RED**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/ShipmentSearch
```

Expected: FAIL because the search and layout modules do not exist.

- [ ] **Step 3: Implement the pure search and controlled keyboard**

```ts
export function searchShipments(tasks, query, locale) {
  const normalized = query.trim().toLocaleLowerCase(locale === 'ru' ? 'ru-RU' : 'en-US');
  if (!normalized) return [];
  return tasks.filter(({ shipmentNumber }) =>
    shipmentNumber.toLocaleLowerCase(locale === 'ru' ? 'ru-RU' : 'en-US').includes(normalized),
  );
}
```

Keyboard press behavior stays controlled:

```ts
const append = (character: string) => onQueryChange(`${query}${character}`);
const removeLast = () => onQueryChange(Array.from(query).slice(0, -1).join(''));
const clear = () => onQueryChange('');
```

The query field is a visible controlled text input. Every on-screen key is a real `Button` with an accessible label. `Удалить`, `Очистить`, `123`, `АБВ`, `ABC`, and `Символы` are explicit controls rather than long-press gestures.

- [ ] **Step 4: Implement no-scroll result pagination**

Use `resultsPerPage={3}` for the 1280 story. Normalize the page after every query change and expose explicit previous/next buttons. Render `ShipmentTaskCard` with consignee and planned date for every result.

```ts
const results = searchShipments(tasks, query, locale);
const pageCount = Math.max(1, Math.ceil(results.length / resultsPerPage));
const normalizedPage = Math.min(page, pageCount - 1);
const pageResults = results.slice(
  normalizedPage * resultsPerPage,
  normalizedPage * resultsPerPage + resultsPerPage,
);
```

When `query.trim()` is empty, show the localized prompt. When results are empty, use `EmptyState` with the literal query in the description.

Create a controlled `ShipmentSearch.stories.tsx` and retain this browser contract:

```tsx
export const PartialResults: Story = {
  args: { tasks: shipmentTasks, query: '842', layer: 'numeric', page: 0, resultsPerPage: 3 },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getAllByRole('button', { name: /Отгрузка РН-842/ })).toHaveLength(3);
    await userEvent.click(canvas.getByRole('button', { name: 'ABC' }));
    await expect(args.onLayerChange).toHaveBeenCalledWith('latin');
    await userEvent.click(canvas.getByRole('button', { name: 'Очистить' }));
    await expect(args.onQueryChange).toHaveBeenCalledWith('');
  },
};
```

- [ ] **Step 5: Verify unit, type, and declaration boundaries**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/ShipmentSearch
CI=true corepack pnpm test:storybook -- --grep ShipmentSearch
corepack pnpm --filter @puntiro/ui typecheck
corepack pnpm --filter @puntiro/ui build
```

Expected: all focused tests PASS; emitted declarations expose only Puntiro types.

- [ ] **Step 6: Commit**

```bash
git add packages/ui/src apps/storybook/src/stories/Compositions/ShipmentSearch.stories.tsx
git commit -m "feat: add touch shipment search"
```

---

### Task 5: Build the Direct Place-Count Step

**Files:**
- Create: `packages/ui/src/patterns/PlaceCountStep/PlaceCountStep.types.ts`
- Create: `packages/ui/src/patterns/PlaceCountStep/PlaceCountStep.tsx`
- Create: `packages/ui/src/patterns/PlaceCountStep/PlaceCountStep.module.css`
- Create: `packages/ui/src/patterns/PlaceCountStep/PlaceCountStep.test.tsx`
- Create: `packages/ui/src/patterns/PlaceCountStep/placeCountModel.ts`
- Create: `packages/ui/src/patterns/PlaceCountStep/placeCountModel.test.ts`
- Create: `packages/ui/src/patterns/PlaceCountStep/placeCountCopy.ts`
- Modify: `apps/storybook/src/stories/Compositions/fixtures.ts`
- Create: `apps/storybook/src/stories/Compositions/PlaceCountStep.stories.tsx`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**

```ts
import type { PrinterOption } from '../PrinterPicker/PrinterPicker.types';

export type PlaceCountAction =
  | { kind: 'print'; printer: PrinterOption; onPress: () => void }
  | { kind: 'choose-printer'; onPress: () => void };

export interface PlaceCountStepProps {
  shipment: ShipmentTaskSummary;
  value: number | null;
  action: PlaceCountAction;
  onChange: (value: number | null) => void;
  onBack: () => void;
}
```

The component owns only the approved `1..100` validation and `>10` confirmation presentation. It does not select printers, create label sets, or call print hardware.

- [ ] **Step 1: Write failing model and SSR contracts**

```ts
it.each([
  [null, 5, 5],
  [5, 0, 50],
  [50, 0, 50],
] as const)('appends %s + %s as %s', (current, digit, expected) => {
  expect(appendPlaceCountDigit(current, digit)).toBe(expected);
});

it.each([[10, false], [11, true], [100, true]] as const)(
  'requires confirmation for %i: %s',
  (value, expected) => expect(requiresLargeCountConfirmation(value)).toBe(expected),
);
```

```tsx
const shipment: ShipmentTaskSummary = {
  id: 'shipment-842711',
  shipmentNumber: 'РН-842711',
  consigneeName: 'ООО Северный распределительный центр',
  plannedShipAt: new Date('2026-08-08T09:00:00Z'),
  status: 'ready',
};
const readyPrinter: PrinterOption = {
  id: 'zebra-zd421',
  name: 'Zebra ZD421',
  language: 'zpl',
  state: 'ready',
};

it('renders direct-print context without a review step', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><PlaceCountStep
      shipment={shipment}
      value={5}
      action={{ kind: 'print', printer: readyPrinter, onPress: () => undefined }}
      onChange={() => undefined}
      onBack={() => undefined}
    /></PuntiroProvider>,
  );
  expect(html).toContain('РН-842711');
  expect(html).toContain('ООО Северный распределительный центр');
  expect(html).toContain('Напечатать 5 этикеток · Zebra ZD421');
  expect(html).toContain('data-primary-action="true"');
  expect(html).not.toContain('Проверьте перед печатью');
});
```

- [ ] **Step 2: Confirm RED**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/PlaceCountStep
```

Expected: FAIL because the model and `PlaceCountStep` do not exist.

- [ ] **Step 3: Implement numeric input and exact action copy**

Render full shipment number, full consignee, planned date, a dominant value, and a 3-by-4 numeric keypad. Use `Button variant="secondary"` for digits and utilities and the default primary `Button` for the single forward action. `Button` supplies `data-primary-action="true"` internally.

Put value updates in `placeCountModel.ts`:

```ts
export function appendPlaceCountDigit(value: number | null, digit: number): number | null {
  const next = Number(`${value ?? ''}${digit}`);
  return next >= 1 && next <= 100 ? next : value;
}

export function removePlaceCountDigit(value: number | null): number | null {
  if (value === null) return null;
  const next = String(value).slice(0, -1);
  return next ? Number(next) : null;
}

export function requiresLargeCountConfirmation(value: number): boolean {
  return value > 10;
}
```

Use `Intl.PluralRules` through `placeCountCopy.ts` to produce `1 этикетку`, `2 этикетки`, `5 этикеток` and EN `1 label`, `5 labels`.

Primary behavior uses the existing `Dialog` trigger contract:

```tsx
const isValid = value !== null && value >= 1 && value <= 100;
const requiresConfirmation = isValid && requiresLargeCountConfirmation(value);
const primary = <Button
  isDisabled={!isValid}
  onPress={requiresConfirmation ? undefined : action.onPress}
>
  {primaryLabel}
</Button>;

const submitControl = requiresConfirmation ? <Dialog
  trigger={primary}
  title={copy.confirmTitle(value)}
  description={copy.confirmDescription}
  actions={[
    { id: 'change', label: copy.change, variant: 'secondary', onPress: (close) => close() },
    { id: 'confirm', label: copy.confirmAction(action.kind, value), variant: 'primary', onPress: (close) => {
      action.onPress();
      close();
    } },
  ]}
>
  <p>{copy.confirmContext(value)}</p>
</Dialog> : primary;
```

The dialog actions are exactly `Изменить` and `Да, напечатать 24` for `print`, or `Да, выбрать принтер` for `choose-printer`.

- [ ] **Step 4: Add Chromium interaction stories**

Extend `Compositions/fixtures.ts` with:

```ts
export const readyPrinter: PrinterOption = {
  id: 'zebra-zd421',
  name: 'Zebra ZD421',
  language: 'zpl',
  state: 'ready',
};
```

Create the story with missing-export RED first, then retain these plays after implementation:

```tsx
export const OnePrinter: Story = {
  args: { value: 5, action: { kind: 'print', printer: readyPrinter, onPress: fn() } },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByText('ООО Северный распределительный центр')).toBeVisible();
    await userEvent.click(canvas.getByRole('button', { name: 'Напечатать 5 этикеток · Zebra ZD421' }));
    await expect(args.action.onPress).toHaveBeenCalledOnce();
  },
};

export const LargeCount: Story = {
  args: { value: 11, action: { kind: 'print', printer: readyPrinter, onPress: fn() } },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Напечатать 11 этикеток · Zebra ZD421' }));
    const dialog = within(document.body).getByRole('dialog', { name: 'Напечатать 11 этикеток?' });
    await expect(args.action.onPress).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Да, напечатать 11' }));
    await expect(args.action.onPress).toHaveBeenCalledOnce();
  },
};
```

Add SSR/model cases for null, 1, 10, 11, 100, attempts above 100, delete-to-null, RU/EN pluralization, and semantic-only CSS. The CSS contract is:

```ts
expect(patternCss).not.toContain('--puntiro-reference-');
expect(buttonCss).toContain('--puntiro-component-button-primary-min-block-size');
```

- [ ] **Step 5: Verify and commit**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/PlaceCountStep
CI=true corepack pnpm test:storybook -- --grep PlaceCountStep
corepack pnpm --filter @puntiro/ui typecheck
corepack pnpm --filter @puntiro/ui build
git add packages/ui/src apps/storybook/src/stories/Compositions/PlaceCountStep.stories.tsx
git commit -m "feat: add direct place count step"
```

Expected: focused tests, typecheck, UI build, and declaration scan PASS.

---

### Task 6: Build Paginated Printer Selection with Direct Print

**Files:**
- Modify: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.types.ts`
- Modify: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.tsx`
- Modify: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.module.css`
- Modify: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.test.tsx`
- Create: `packages/ui/src/patterns/PrinterSelectionStep/PrinterSelectionStep.types.ts`
- Create: `packages/ui/src/patterns/PrinterSelectionStep/PrinterSelectionStep.tsx`
- Create: `packages/ui/src/patterns/PrinterSelectionStep/PrinterSelectionStep.module.css`
- Create: `packages/ui/src/patterns/PrinterSelectionStep/PrinterSelectionStep.test.tsx`
- Create: `packages/ui/src/patterns/PrinterSelectionStep/printerPagination.ts`
- Create: `packages/ui/src/patterns/PrinterSelectionStep/printerPagination.test.ts`
- Create: `apps/storybook/src/stories/Compositions/PrinterSelectionStep.stories.tsx`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**

```ts
export interface PrinterOption {
  id: string;
  name: string;
  location?: string;
  language: PrinterLanguage;
  state: PrinterState;
}

export interface PrinterSelectionStepProps {
  shipment: ShipmentTaskSummary;
  placeCount: number;
  printers: readonly PrinterOption[];
  selectedId?: string;
  page: number;
  pageSize: 4 | 6;
  onSelectionChange: (id: string) => void;
  onPageChange: (page: number) => void;
  onPrint: () => void;
  onBack: () => void;
}
```

- [ ] **Step 1: Write failing pagination and SSR tests**

```ts
const sixPrinters: readonly PrinterOption[] = Array.from({ length: 6 }, (_, index) => ({
  id: `printer-${index}`,
  name: `Printer ${index}`,
  language: index % 2 === 0 ? 'zpl' : 'tspl',
  state: 'ready',
}));

it('returns four of six printers on the first page', () => {
  expect(paginatePrinters(sixPrinters, 0, 4)).toMatchObject({
    page: 0,
    pageCount: 2,
    items: sixPrinters.slice(0, 4),
  });
});

it('bounds an out-of-range page', () => {
  expect(paginatePrinters(sixPrinters, 10, 4).page).toBe(1);
});
```

```tsx
const shipment: ShipmentTaskSummary = {
  id: 'shipment-842711',
  shipmentNumber: 'РН-842711',
  consigneeName: 'ООО Северный распределительный центр',
  plannedShipAt: new Date('2026-08-08T09:00:00Z'),
  status: 'ready',
};
const mixedPrinters: readonly PrinterOption[] = [
  { id: 'zebra', name: 'Zebra ZD421', location: 'Склад, стол 1', language: 'zpl', state: 'ready' },
  { id: 'tsc', name: 'TSC TE200', location: 'Склад, стол 2', language: 'tspl', state: 'busy' },
];

it('renders location and a disabled print action before selection', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><PrinterSelectionStep
      shipment={shipment}
      placeCount={5}
      printers={mixedPrinters}
      page={0}
      pageSize={4}
      onSelectionChange={() => undefined}
      onPageChange={() => undefined}
      onPrint={() => undefined}
      onBack={() => undefined}
    /></PuntiroProvider>,
  );
  expect(html).toContain('Склад, стол 1');
  expect(html).toContain('Напечатать 5 этикеток');
  expect(html).toContain('disabled=""');
});
```

- [ ] **Step 2: Confirm RED**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/PrinterPicker packages/ui/src/patterns/PrinterSelectionStep
```

Expected: FAIL because `location`, `paginatePrinters`, and `PrinterSelectionStep` are absent.

- [ ] **Step 3: Extend PrinterPicker and implement the step**

Render `location` as visible secondary text and include it in the option description. Keep the existing React Aria radio-group semantics.

Step pagination:

```ts
export function paginatePrinters(printers: readonly PrinterOption[], page: number, pageSize: 4 | 6) {
  const pageCount = Math.max(1, Math.ceil(printers.length / pageSize));
  const normalizedPage = Math.min(Math.max(page, 0), pageCount - 1);
  return {
    page: normalizedPage,
    pageCount,
    items: printers.slice(normalizedPage * pageSize, normalizedPage * pageSize + pageSize),
  } as const;
}
```

Render the shipment number and `N мест`, then `PrinterPicker`, explicit page controls when `pageCount > 1`, and one primary button. The primary button is disabled until `selectedId` identifies a ready visible or non-visible printer in the full input list.

```tsx
<Button isDisabled={!selectedPrinter || selectedPrinter.state !== 'ready'} onPress={onPrint}>
  {copy.print(placeCount)}
</Button>
```

- [ ] **Step 4: Add exact API and Chromium interaction tests**

Extend the existing contract test to include `location?: string`, exact step props, no `className`/`style`, and no React Aria types. Keep arrow-key radio selection coverage and test that page navigation does not erase `selectedId`.

```ts
const stepPropsAreExact: Equal<PrinterSelectionStepProps, {
  shipment: ShipmentTaskSummary;
  placeCount: number;
  printers: readonly PrinterOption[];
  selectedId?: string;
  page: number;
  pageSize: 4 | 6;
  onSelectionChange: (id: string) => void;
  onPageChange: (page: number) => void;
  onPrint: () => void;
  onBack: () => void;
}> = true;
void stepPropsAreExact;

for (const source of [printerTypes, stepTypes]) {
  expect(source).not.toContain('react-aria-components');
  expect(source).not.toMatch(/\bclassName\??:/);
  expect(source).not.toMatch(/\bstyle\??:/);
}
```

Add `PrinterSelectionStep.stories.tsx` with:

```ts
const sixPrinters: readonly PrinterOption[] = Array.from({ length: 6 }, (_, index) => ({
  id: `printer-${index}`,
  name: `Printer ${index}`,
  location: `Стол ${index + 1}`,
  language: index % 2 === 0 ? 'zpl' : 'tspl',
  state: 'ready',
}));
```

```tsx
export const Default: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    const print = canvas.getByRole('button', { name: 'Напечатать 5 этикеток' });
    await expect(print).toBeDisabled();
    await userEvent.click(canvas.getByRole('radio', { name: /Zebra ZD421/ }));
    await expect(args.onSelectionChange).toHaveBeenCalledWith('zebra-zd421');
  },
};

export const SixPrinters: Story = {
  args: { printers: sixPrinters, pageSize: 4 },
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getAllByRole('radio')).toHaveLength(4);
    await userEvent.click(canvas.getByRole('button', { name: 'Следующая страница' }));
    await expect(args.onPageChange).toHaveBeenCalledWith(1);
  },
};
```

- [ ] **Step 5: Verify and commit**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/PrinterPicker packages/ui/src/patterns/PrinterSelectionStep apps/storybook/src/stories/KioskPatterns/placeAndPrinter.contract.test.ts
CI=true corepack pnpm test:storybook -- --grep "PrinterPicker|PrinterSelectionStep"
corepack pnpm --filter @puntiro/ui build
git add packages/ui/src apps/storybook/src/stories/KioskPatterns apps/storybook/src/stories/Compositions/PrinterSelectionStep.stories.tsx
git commit -m "feat: add direct printer selection step"
```

Expected: unit/contract, Chromium stories, UI build, and declaration scan PASS.

---

### Task 7: Build Locked Printing and Success Steps

**Files:**
- Create: `packages/ui/src/patterns/PrintingStep/PrintingStep.types.ts`
- Create: `packages/ui/src/patterns/PrintingStep/PrintingStep.tsx`
- Create: `packages/ui/src/patterns/PrintingStep/PrintingStep.module.css`
- Create: `packages/ui/src/patterns/PrintingStep/PrintingStep.test.tsx`
- Create: `packages/ui/src/patterns/PrintSuccess/PrintSuccess.types.ts`
- Create: `packages/ui/src/patterns/PrintSuccess/PrintSuccess.tsx`
- Create: `packages/ui/src/patterns/PrintSuccess/PrintSuccess.module.css`
- Create: `packages/ui/src/patterns/PrintSuccess/PrintSuccess.test.tsx`
- Create: `apps/storybook/src/stories/Compositions/PrintingStep.stories.tsx`
- Create: `apps/storybook/src/stories/Compositions/PrintSuccess.stories.tsx`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**

```ts
export interface PrintingStepProps {
  shipmentNumber: string;
  completed: number;
  total: number;
  printerName: string;
  printerLanguage: PrinterLanguage;
}

export interface PrintSuccessProps {
  shipmentNumber: string;
  placeCount: number;
  printerName: string;
  secondsRemaining: number;
  onReturn: () => void;
}
```

Neither component owns a timer. The future application supplies `secondsRemaining`; Storybook simulates it in the harness.

- [ ] **Step 1: Write failing SSR contracts**

```tsx
it('renders printing with no interactive escape', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><PrintingStep
      shipmentNumber="РН-842711"
      completed={3}
      total={5}
      printerName="Zebra ZD421"
      printerLanguage="zpl"
    /></PuntiroProvider>,
  );
  expect(html).toContain('3 из 5');
  expect(html).toContain('ZPL');
  expect(html).toContain('role="progressbar"');
  expect(html).not.toContain('<button');
});

it('renders success as one status with an early-return action', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><PrintSuccess
      shipmentNumber="РН-842711"
      placeCount={5}
      printerName="Zebra ZD421"
      secondsRemaining={3}
      onReturn={() => undefined}
    /></PuntiroProvider>,
  );
  expect(html).toContain('role="status"');
  expect(html).toContain('5 этикеток напечатано');
  expect(html).toContain('Возврат к отгрузкам через 3 секунды');
  expect(html).toContain('К отгрузкам');
  expect(html.match(/role="status"/gu)).toHaveLength(1);
});
```

- [ ] **Step 2: Confirm RED**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/PrintingStep packages/ui/src/patterns/PrintSuccess
```

Expected: FAIL because both patterns are absent.

- [ ] **Step 3: Implement with existing PrintProgress and Button**

`PrintingStep` composes `PrintProgress` and visible language text. Its root has `data-kiosk-working-region="true"` and contains no controls.

`PrintSuccess` uses a private visual success mark with `aria-hidden="true"`, while the visible text is the `role="status"` content. It uses semantic success text on a standard surface; it does not fill the whole screen green.

```tsx
<section className={styles.root} data-kiosk-working-region="true">
  <div className={styles.mark} aria-hidden="true"><PuntiroIcon name="check" /></div>
  <div role="status" aria-live="polite">
    <h1>{copy.completed(placeCount)}</h1>
    <p>{shipmentNumber}</p>
    <p>{copy.printer(printerName)}</p>
  </div>
  <p>{copy.returnIn(secondsRemaining)}</p>
  <Button onPress={onReturn}>{copy.returnNow}</Button>
</section>
```

- [ ] **Step 4: Test localization and browser interaction**

Add SSR cases for `0 из 5`, `3 из 5`, and `5 из 5`; RU/EN plural copy; seconds 1/2/5; and exactly one success live region. Add long-number and reduced-motion browser stories.

```tsx
it.each([[0, '0 из 5'], [3, '3 из 5'], [5, '5 из 5']] as const)(
  'renders discrete progress %i of 5',
  (completed, expected) => {
    const html = renderToStaticMarkup(
      <PuntiroProvider><PrintingStep
        shipmentNumber="РН-842711"
        completed={completed}
        total={5}
        printerName="Zebra ZD421"
        printerLanguage="zpl"
      /></PuntiroProvider>,
    );
    expect(html).toContain(expected);
  },
);
```

```tsx
it.each([
  ['ru', 1, '1 этикетка напечатана', 'через 1 секунду'],
  ['ru', 5, '5 этикеток напечатано', 'через 5 секунд'],
  ['en', 1, '1 label printed', 'in 1 second'],
  ['en', 5, '5 labels printed', 'in 5 seconds'],
] as const)('localizes %s success for %i places', (locale, placeCount, result, countdown) => {
  const html = renderToStaticMarkup(
    <PuntiroProvider locale={locale}><PrintSuccess
      shipmentNumber="РН-842711"
      placeCount={placeCount}
      printerName="Zebra ZD421"
      secondsRemaining={placeCount}
      onReturn={() => undefined}
    /></PuntiroProvider>,
  );
  expect(html).toContain(result);
  expect(html).toContain(countdown);
  expect(html.match(/role="status"/gu)).toHaveLength(1);
});
```

Add a `PrintSuccess` story play that clicks `К отгрузкам` and asserts `onReturn` once. Add a `PrintingStep` story play that asserts `3 из 5`, `aria-valuenow="3"`, zero buttons, and visible `ZPL`. Add long-number and reduced-motion stories for both fixed states.

```tsx
export const Default: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await expect(canvas.getByRole('status')).toHaveTextContent('5 этикеток напечатано');
    await userEvent.click(canvas.getByRole('button', { name: 'К отгрузкам' }));
    await expect(args.onReturn).toHaveBeenCalledOnce();
  },
};
```

- [ ] **Step 5: Verify and commit**

```bash
corepack pnpm exec vitest run packages/ui/src/patterns/PrintingStep packages/ui/src/patterns/PrintSuccess
CI=true corepack pnpm test:storybook -- --grep "PrintingStep|PrintSuccess"
corepack pnpm --filter @puntiro/ui typecheck
corepack pnpm --filter @puntiro/ui build
git add packages/ui/src apps/storybook/src/stories/Compositions/PrintingStep.stories.tsx apps/storybook/src/stories/Compositions/PrintSuccess.stories.tsx
git commit -m "feat: add printing and success steps"
```

Expected: all focused tests, typecheck, UI build, and declaration scan PASS.

---

### Task 8: Publish the Happy Path as Storybook Compositions

**Files:**
- Modify: `apps/storybook/src/stories/Compositions/fixtures.ts`
- Create: `apps/storybook/src/stories/Compositions/KioskCoreFlowHarness.tsx`
- Modify: `apps/storybook/src/stories/Compositions/ShipmentQueue.stories.tsx`
- Modify: `apps/storybook/src/stories/Compositions/ShipmentSearch.stories.tsx`
- Modify: `apps/storybook/src/stories/Compositions/PlaceCountStep.stories.tsx`
- Modify: `apps/storybook/src/stories/Compositions/PrinterSelectionStep.stories.tsx`
- Modify: `apps/storybook/src/stories/Compositions/PrintingStep.stories.tsx`
- Modify: `apps/storybook/src/stories/Compositions/PrintSuccess.stories.tsx`
- Create: `apps/storybook/src/stories/Compositions/CoreFlow.stories.tsx`
- Create: `apps/storybook/src/docs/compositions/CoreFlow.mdx`
- Modify: `apps/storybook/src/docs/content.ru.ts`
- Modify: `apps/storybook/src/docs/content.en.ts`
- Modify: `apps/storybook/src/docs/articles.test.ts`
- Modify: `apps/storybook/src/docs/maturity-manifest.ts`
- Modify: `apps/storybook/src/docs/maturity-manifest.test.ts`
- Modify: `package.json`
- Modify: `packages/tokens/package.json`
- Modify: `packages/ui/package.json`
- Modify: `apps/storybook/package.json`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**

The Storybook-only harness owns this state, never exported by `@puntiro/ui`:

```ts
type CoreFlowStep = 'queue' | 'search' | 'count' | 'printer' | 'printing' | 'success';

interface CoreFlowState {
  step: CoreFlowStep;
  queuePage: number;
  searchQuery: string;
  selectedShipmentId?: string;
  placeCount: number | null;
  selectedPrinterId?: string;
  completed: number;
  secondsRemaining: number;
}

interface KioskCoreFlowHarnessProps {
  printers: readonly PrinterOption[];
  autoAdvancePrinting?: boolean;
  progressTickMs?: number;
  autoAdvanceSuccess?: boolean;
  countdownTickMs?: number;
}
```

- [ ] **Step 1: Write RED harness transitions**

Extend the deterministic `fixtures.ts` created in Task 5 so every composition imports the same values:

```ts
export const shipmentTasks: readonly ShipmentTaskSummary[] = Array.from({ length: 13 }, (_, index) => ({
  id: `shipment-${index}`,
  shipmentNumber: index === 0 ? 'РН-842711' : `РН-${842710 - index}`,
  consigneeName: index === 0
    ? 'ООО Северный распределительный центр'
    : `Грузополучатель ${index + 1}`,
  plannedShipAt: new Date(`2026-08-08T${String(9 + (index % 8)).padStart(2, '0')}:00:00Z`),
  status: index === 3 ? 'updated' : 'ready',
}));

export const readyPrinter: PrinterOption = {
  id: 'zebra-zd421',
  name: 'Zebra ZD421',
  location: 'Склад, стол 1',
  language: 'zpl',
  state: 'ready',
};

export const readyPrinters: readonly PrinterOption[] = [
  readyPrinter,
  { id: 'tsc-te200', name: 'TSC TE200', location: 'Склад, стол 2', language: 'tspl', state: 'ready' },
];
```

Then create `CoreFlow.stories.tsx` with browser RED against the not-yet-created harness:

```tsx
async function enterPlaceCount(canvas: ReturnType<typeof within>, value: number) {
  for (const digit of String(value)) {
    await userEvent.click(canvas.getByRole('button', { name: digit }));
  }
}

export const InteractiveHappyPath: Story = {
  args: { printers: [readyPrinter] },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: /РН-842711/ }));
    await enterPlaceCount(canvas, 5);
    await userEvent.click(canvas.getByRole('button', { name: /Напечатать 5 этикеток · Zebra ZD421/ }));
    await expect(canvas.getByText('Печатается место')).toBeVisible();
    await expect(canvas.queryByText('Выберите принтер')).not.toBeInTheDocument();
  },
};

export const MultiplePrinters: Story = {
  args: { printers: readyPrinters },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: /РН-842711/ }));
    await enterPlaceCount(canvas, 5);
    await userEvent.click(canvas.getByRole('button', { name: 'Выбрать принтер' }));
    await expect(canvas.getByRole('radiogroup', { name: 'Выберите принтер' })).toBeVisible();
  },
};
```

- [ ] **Step 2: Confirm RED**

```bash
CI=true corepack pnpm test:storybook -- --grep CoreFlow
```

Expected: FAIL because `KioskCoreFlowHarness` does not exist.

- [ ] **Step 3: Implement the deterministic callback harness**

Use local React state only. `onPrint` changes the step to `printing`; it does not call hardware. Model print progress only when `autoAdvancePrinting` is true, and model the success countdown only when `autoAdvanceSuccess` is true:

```ts
const startPrint = () => setState((current) => ({ ...current, step: 'printing', completed: 0 }));
const returnToQueue = () => setState((current) => ({ ...initialState, queuePage: current.queuePage }));
```

Progress and countdown effects:

```tsx
useEffect(() => {
  if (!autoAdvancePrinting || state.step !== 'printing') return;
  const timeoutId = window.setTimeout(() => {
    setState((current) => {
      const total = current.placeCount ?? 1;
      const completed = Math.min(total, current.completed + 1);
      return completed === total
        ? { ...current, step: 'success', completed, secondsRemaining: 3 }
        : { ...current, completed };
    });
  }, progressTickMs);
  return () => window.clearTimeout(timeoutId);
}, [autoAdvancePrinting, progressTickMs, state.completed, state.step]);

useEffect(() => {
  if (!autoAdvanceSuccess || state.step !== 'success') return;
  if (state.secondsRemaining === 0) {
    returnToQueue();
    return;
  }

  const timeoutId = window.setTimeout(() => {
    setState((current) => ({
      ...current,
      secondsRemaining: Math.max(0, current.secondsRemaining - 1),
    }));
  }, countdownTickMs);

  return () => window.clearTimeout(timeoutId);
}, [autoAdvanceSuccess, countdownTickMs, state.step, state.secondsRemaining]);
```

Add a fast browser story with both tick durations set to 10 ms that verifies automatic progress, automatic return, and the preserved `queuePage`. Fixed-state stories render the public pattern directly instead of depending on harness timing.

```tsx
async function startHarnessPrintOnPageTwo(canvas: ReturnType<typeof within>) {
  await userEvent.click(canvas.getByRole('button', { name: 'Следующая страница' }));
  await userEvent.click(canvas.getAllByRole('button', { name: /Отгрузка/ })[0]);
  await enterPlaceCount(canvas, 5);
  await userEvent.click(canvas.getByRole('button', { name: /Напечатать 5 этикеток/ }));
}

export const AutomaticReturn: Story = {
  args: {
    printers: [readyPrinter],
    autoAdvancePrinting: true,
    progressTickMs: 10,
    autoAdvanceSuccess: true,
    countdownTickMs: 10,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await startHarnessPrintOnPageTwo(canvas);
    await waitFor(() => expect(canvas.getByRole('heading', { name: 'Отгрузки' })).toBeVisible());
    await expect(canvas.getByText('2 / 3')).toBeVisible();
  },
};
```

- [ ] **Step 4: Create fixed RU/EN-ready stories**

Use these stable story IDs:

- `compositions-shipment-queue--default`
- `compositions-shipment-queue--long-content`
- `compositions-shipment-search--partial-results`
- `compositions-place-count-step--one-printer`
- `compositions-place-count-step--multiple-printers`
- `compositions-printer-selection-step--default`
- `compositions-printing-step--partial`
- `compositions-print-success--default`
- `compositions-core-flow--interactive-happy-path`
- `compositions-core-flow--reduced-motion`

Every screen story sets `tags: ['test']`, `globals: { interactionMode: 'touch' }`, and wraps the pattern with `KioskShell`. Processing stories set `showNavigation={false}`.

Each story play function asserts its dominant number, full consignee where applicable, one primary action where applicable, and absence of page overflow at the DOM contract level. `ReducedMotion` uses `reducedMotionParameters` and asserts both inherited motion durations resolve to `0s` through `expectReducedMotionEnvironment`.

Use this exact story structure for every fixed screen:

```tsx
const meta = {
  title: 'Compositions/Shipment queue',
  component: ShipmentQueue,
  tags: ['test'],
  globals: { interactionMode: 'touch' },
  args: {
    tasks: shipmentTasks,
    page: 0,
    anchorShipmentId: undefined,
    onPageChange: fn(),
    onOpenShipment: fn(),
    onOpenSearch: fn(),
  },
  render: (args) => <KioskShell
    connectionState="online"
    printerSummary="Zebra ZD421 готов"
    activeSection="shipments"
    onSectionChange={fn()}
  ><ShipmentQueue {...args} /></KioskShell>,
} satisfies Meta<typeof ShipmentQueue>;

export default meta;
export const Default: StoryObj<typeof meta> = {};
```

- [ ] **Step 5: Replace future-only composition documentation**

Add `composition.coreFlow` to both content dictionaries. RU copy must state:

```ts
'composition.coreFlow': {
  title: 'Основной сценарий киоска',
  lead: 'Утверждённые композиции соединяют очередь, поиск, количество, выбор принтера, печать и успех без отдельного экрана проверки.',
  sections: [
    { title: 'Прямой путь', body: 'При одном готовом принтере количество сразу запускает печать. При нескольких устройствах добавляется только необходимый выбор принтера.' },
    { title: 'Граница композиции', body: 'Данные и callbacks передаёт приложение. HTTP, persistence, routing, Tauri, timers и hardware adapters не входят в Storybook-композицию.' },
    { title: 'Следующие состояния', body: 'Архив, повтор, offline, failed, partial и unknown result проектируются и утверждаются отдельными потоками.' },
  ],
}
```

Create `CoreFlow.mdx` using `<Meta title="Compositions/Core flow" />` and `<LocalizedArticle id="composition.coreFlow" />`. Update the old introduction so it no longer claims that product screens do not exist. Update `articles.test.ts` to expect the new article and the new boundary wording.

- [ ] **Step 6: Register new Beta exports**

Set the root, tokens, UI, and Storybook package versions to `0.2.0`, and update `puntiroUiVersion` to the same literal. Keep dependency ranges unchanged because workspace links remain `workspace:*`.

Add manifest entries since `0.2.0`, with both manual flags false:

```ts
beta('KioskShell', 'compositions-shipment-queue--default', '0.2.0'),
beta('ShipmentQueue', 'compositions-shipment-queue--default', '0.2.0'),
beta('ShipmentSearch', 'compositions-shipment-search--partial-results', '0.2.0'),
beta('PlaceCountStep', 'compositions-place-count-step--one-printer', '0.2.0'),
beta('PrinterSelectionStep', 'compositions-printer-selection-step--default', '0.2.0'),
beta('PrintingStep', 'compositions-printing-step--partial', '0.2.0'),
beta('PrintSuccess', 'compositions-print-success--default', '0.2.0'),
```

Change the `beta` helper to accept `since = '0.1.0'`. Rename `FirstWaveExportName` to `DocumentedExportName`, update bindings, and assert 26 total documented exports: 19 existing plus 7 new.

Add a version-alignment assertion:

```ts
const secondWaveNames = [
  'KioskShell',
  'ShipmentQueue',
  'ShipmentSearch',
  'PlaceCountStep',
  'PrinterSelectionStep',
  'PrintingStep',
  'PrintSuccess',
] as const;

it('aligns second-wave maturity with the platform version', () => {
  expect(puntiroUiVersion).toBe('0.2.0');
  for (const entry of maturityManifest.filter(
    ({ name }) => secondWaveNames.includes(name as (typeof secondWaveNames)[number]),
  )) {
    expect(entry.since).toBe('0.2.0');
  }
});
```

- [ ] **Step 7: Verify docs, harness, and Storybook GREEN**

```bash
corepack pnpm exec vitest run apps/storybook/src/docs/articles.test.ts apps/storybook/src/docs/maturity-manifest.test.ts
CI=true corepack pnpm test:storybook
corepack pnpm storybook:build
```

Expected: focused unit tests PASS, all tagged Storybook tests PASS in Chromium, static Storybook build exit 0.

- [ ] **Step 8: Commit**

```bash
git add package.json packages/tokens/package.json packages/ui/package.json packages/ui/src/index.ts apps/storybook/package.json apps/storybook/src
git commit -m "feat: publish kiosk core flow compositions"
```

---

### Task 9: Add Responsive, Interaction, and Visual Acceptance Contracts

**Files:**
- Modify: `apps/storybook/tests/helpers.ts`
- Modify: `apps/storybook/tests/visual-cases.ts`
- Modify: `apps/storybook/tests/visual.spec.ts`
- Create: `apps/storybook/tests/kiosk-core-flow.spec.ts`
- Create: `apps/storybook/tests/visual.spec.ts-snapshots/compositions-*.png`
- Modify: `README.md`

**Interfaces:**

Extend visual cases with viewport:

```ts
export interface VisualCase {
  id: string;
  locale: StoryLocale;
  mode: StoryInteractionMode;
  viewport?: { width: number; height: number };
}
```

Snapshot names include viewport only when it differs from `1280 x 800`:

```ts
const suffix = visualCase.viewport
  ? `-${visualCase.viewport.width}x${visualCase.viewport.height}`
  : '';
return `${visualCase.id}-${visualCase.locale}-${visualCase.mode}${suffix}.png`;
```

- [ ] **Step 1: Write failing responsive Playwright contracts**

Create `kiosk-core-flow.spec.ts`:

```ts
for (const [width, height, expected] of [
  [1280, 800, 6],
  [1600, 900, 9],
  [1920, 1080, 12],
] as const) {
  test(`queue renders ${expected} cards at ${width}x${height}`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    await openStory(page, 'compositions-shipment-queue--default', 'ru', 'touch');
    await expect(page.locator('[data-queue-capacity]')).toHaveAttribute('data-queue-capacity', String(expected));
    await expect(page.getByRole('button', { name: /Отгрузка/ })).toHaveCount(expected);
    await assertKioskContract(page);
  });
}
```

Add tests that:

- search `842` returns only matching shipment numbers in source order;
- the long-number and long-consignee queue story has no clipping or overflow in RU and EN;
- the `[data-puntiro-logo="true"]` mark has a non-zero box and remains visible on the light health bar;
- resizing the queue keeps `anchorShipmentId` on the visible page and removing the final card never leaves an empty out-of-range page;
- keyboard layer controls reach numeric, Cyrillic, Latin, and symbols;
- back from search preserves the queue page;
- one-printer count story has no printer-selection screen and invokes print once;
- multiple-printer story requires a radio selection before print;
- count 11 opens the confirmation dialog while count 10 does not;
- printing has zero buttons and visible `3 из 5`;
- success exposes `role=status` and `К отгрузкам`;
- the interactive harness returns to the preserved queue page after its three-second modeled countdown;
- the reduced-motion story resolves both semantic motion durations to zero;
- all screen roots have zero overflow and all actionable targets are at least 64 px.

- [ ] **Step 2: Confirm Playwright RED**

```bash
CI=true corepack pnpm test:visual -- --grep "queue renders|core flow"
```

Expected: FAIL because the new test file, viewport-aware cases, and baselines are absent or incomplete.

- [ ] **Step 3: Make helper and visual-matrix changes**

Before `page.goto`, apply an optional viewport in `openStory` or the calling test. Add these baseline cases:

```ts
const compositionStoryIds = [
  'compositions-shipment-queue--default',
  'compositions-shipment-queue--long-content',
  'compositions-shipment-search--partial-results',
  'compositions-place-count-step--one-printer',
  'compositions-printer-selection-step--default',
  'compositions-printing-step--partial',
  'compositions-print-success--default',
] as const;

const compositionCases = locales.flatMap((locale) =>
  compositionStoryIds.map((id) => ({ id, locale, mode: 'touch' as const })),
);

const responsiveQueueCases = [
  { id: 'compositions-shipment-queue--default', locale: 'ru', mode: 'touch', viewport: { width: 1600, height: 900 } },
  { id: 'compositions-shipment-queue--default', locale: 'ru', mode: 'touch', viewport: { width: 1920, height: 1080 } },
] as const;
```

- [ ] **Step 4: Run focused browser tests and create baselines**

```bash
CI=true corepack pnpm test:visual -- --grep "queue renders|core flow" --update-snapshots
CI=true corepack pnpm test:visual -- --grep "queue renders|core flow"
```

Expected: first run creates only new composition baselines; second clean run PASS. Existing 16 first-wave baselines and token-gallery baseline remain byte-unchanged.

- [ ] **Step 5: Inspect every new baseline**

Inspect the original-resolution PNGs and record in the task report:

- logo visible at normal zoom;
- dominant shipment number;
- consignee readable and not clipped;
- one orange primary action at most;
- no dark product theme;
- no clipped RU/EN copy;
- 6/9/12 queue capacity correct;
- no page or working-region scroll;
- progress and success remain light Operational Calm compositions.

Do not mark Windows, glove, NVDA, or hardware acceptance complete.

- [ ] **Step 6: Document execution boundaries in README**

Add a `Kiosk core flow compositions` section with:

```markdown
The Compositions section contains the approved presentational happy path. It simulates callbacks in Storybook and does not perform routing, persistence, API synchronization, Tauri calls, timers, or printer I/O.

Automated acceptance covers Chromium rendering, RU/EN, keyboard interaction, 64 px targets, no-scroll contracts, reduced motion, and visual baselines at 1280 x 800, 1600 x 900, and 1920 x 1080. Windows, physical touch, gloves, NVDA, ZPL, and TSPL remain manual gates.
```

- [ ] **Step 7: Run the full pinned gate**

```bash
CI=true corepack pnpm check
git diff --check
git status --short
```

Expected:

- token reproducibility PASS;
- recursive typecheck PASS;
- ESLint PASS;
- unit project PASS;
- tagged Storybook Chromium project PASS;
- static Storybook build PASS;
- all Playwright visual and kiosk contracts PASS;
- diff check PASS;
- only the intended implementation files, new baselines, plan/report policy files, and the pre-existing untracked `.pnpm-store/` appear.

- [ ] **Step 8: Commit**

```bash
git add apps/storybook/tests apps/storybook/src README.md
git commit -m "test: verify kiosk core flow compositions"
```

---

## Task Review Gates

Each task is reviewed before the next begins:

1. spec compliance for that task only;
2. public API and type-boundary review;
3. token-only styling review;
4. focused test evidence;
5. diff scope and unrelated-file check.

Any review fix is a separate commit. Do not amend the implementation commit. Run the focused gate again after every fix and run the full `CI=true corepack pnpm check` after Task 9.

## Manual Acceptance Still Required After Automation

- Windows desktop shell at 100%, 125%, and 150% scaling;
- physical 1280 x 800 touch display;
- finger and glove operation;
- working-distance readability of shipment number and consignee;
- NVDA announcements and focus order;
- one real ZPL printer;
- one real TSPL printer;
- multiple connected printers;
- physical unknown-result and disconnect scenarios after their separate design is approved.

These gates stay explicitly false in the maturity manifest until performed and recorded.
