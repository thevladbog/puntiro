import type { ArticleCopy } from './content.ru';

export const enContent = {
  start: {
    title: 'Puntiro Design System Platform',
    lead: 'Exact place. Clear sequence. Confident handoff.',
    sections: [
      { title: 'Living documentation', body: 'Puntiro brings tokens, components, rules, and testable examples together for kiosk and admin work.' },
      { title: 'Working context', body: 'The baseline kiosk runs at 1280 × 800 landscape. Critical actions stay large, clear, and available without hover.' },
      { title: 'Localization', body: 'Russian is the default language. The RU / EN switch changes editorial copy, examples, and system messages while preserving the same structure in both locales.' },
    ],
  },
  'foundation.brand': { title: 'Brand', lead: 'The Puntiro brand marks the precise completion of a shipment.', sections: [{ title: 'P-Sequence', body: 'The registration stem, ordered modules, and terminal orange point show sequence without decorative noise.' }] },
  'foundation.color': { title: 'Color', lead: 'Color guides action and keeps operational contrast.', sections: [{ title: 'One signal', body: 'Handoff Orange identifies a primary action, selection, or current stage. It does not become a large decorative fill.' }] },
  'foundation.typography': { title: 'Typography', lead: 'Onest keeps documentation and interface copy readable.', sections: [{ title: 'Technical data', body: 'IBM Plex Mono is used for shipment numbers, counters, codes, and technical parameters.' }] },
  'foundation.spacing': { title: 'Spacing', lead: 'The system uses an 8 px grid.', sections: [{ title: 'Rhythm', body: 'Use approved spacing values to retain order between labels, controls, and surfaces.' }] },
  'foundation.sizing': { title: 'Sizing and touch targets', lead: 'Element size supports confident finger and gloved operation.', sections: [{ title: 'Modes', body: 'Touch mode has a 64 px minimum target and a 72 px comfortable primary action. Standard mode has a 44 px minimum.' }] },
  'foundation.surface': { title: 'Radius, border, and surface', lead: 'Surfaces are separated through border and space.', sections: [{ title: 'Shape', body: 'Use 4 px for utility labels, 12 px for controls, and 24 px for large surfaces. Glassmorphism is excluded.' }] },
  'foundation.motion': { title: 'Motion', lead: 'Motion confirms an action instead of decorating a page.', sections: [{ title: 'Durations', body: 'Use 120 ms for hover, press, and focus. Use 180 ms to confirm a selection or completed action.' }] },
  'foundation.focus': { title: 'Focus', lead: 'Keyboard focus makes the current interaction point clear.', sections: [{ title: 'Order', body: 'Components retain logical focus order, a visible focus ring, and correct accessible names.' }] },
  'foundation.accessibility': { title: 'Accessibility', lead: 'Touch first does not replace complete keyboard accessibility.', sections: [{ title: 'States', body: 'Loading, empty, offline, invalid, failed, and unknown are product states with a text explanation.' }, { title: 'Icons', body: 'PuntiroIcon encapsulates the approved Lucide glyph set. A critical action has a text label and an accessible name. Emoji never replace icons or labels.' }] },
  'foundation.tokens': { title: 'Token catalog', lead: 'Tokens remain the single source of approved values.', sections: [{ title: 'Three levels', body: 'Reference tokens define scales, semantic tokens define roles, and component tokens exist only for local variation.' }] },
  'rule.levels': {
    title: 'Assembly levels',
    lead: 'Foundation → Component → Pattern → Composition keeps responsibilities explicit before screens are assembled.',
    sections: [
      { title: 'Foundation', body: 'Tokens and rules define values and constraints without behavior of their own.' },
      { title: 'Component', body: 'A component solves one universal task through a domain-independent public API.' },
      { title: 'Pattern', body: 'A pattern combines components for one domain task. It receives data and callbacks but performs no I/O.' },
      { title: 'Composition', body: 'A composition describes a complete working state. APIs, routing, persistence, and hardware remain application concerns.' },
    ],
  },
  'rule.interactionModes': {
    title: 'Interaction modes',
    lead: 'One root mode controls density across the library.',
    sections: [
      { title: 'Touch', body: 'The minimum interactive target is 64 px and the comfortable primary action is 72 px. Critical actions include text and never depend on hover or a hidden gesture.' },
      { title: 'Standard', body: 'The minimum target is 44 px. The mode supports denser admin work while preserving the same semantic and accessibility contracts.' },
      { title: 'Choosing a mode', body: 'PuntiroProvider sets the mode once at the root. Individual components do not repeat size props.' },
    ],
  },
  'rule.kioskLayout': {
    title: 'Kiosk layout',
    lead: '1280 × 800 is the required reference frame; the working region adapts upward.',
    sections: [
      { title: 'No scrolling', body: 'The page and nested working regions of a kiosk composition do not scroll. Documentation pages may use normal scrolling.' },
      { title: 'Persistent context', body: 'Connectivity and print status stay visible, while one primary action remains available without menus or hover.' },
      { title: 'Testable region', body: 'Every working region carries data-kiosk-working-region and is checked for width and height at 1280 × 800 in RU and EN.' },
    ],
  },
  'rule.actionHierarchy': {
    title: 'Action hierarchy',
    lead: 'Every operational state has one obvious primary action.',
    sections: [
      { title: 'Primary', body: 'The primary action is immediately visible, has a text label, and targets a 72 px height in touch mode.' },
      { title: 'Secondary and danger', body: 'Secondary actions support the main path. A dangerous action states its consequence and asks for confirmation without competing with the normal continuation.' },
      { title: 'Dialog', body: 'A dialog supports a decision with clear consequences; it is not hidden navigation or a container for a multi-step flow.' },
    ],
  },
  'rule.content': {
    title: 'Content and long values',
    lead: 'Copy helps the operator recognize the task and choose the next action.',
    sections: [
      { title: 'Shipment number', body: 'The large shipment or sales-document number is the primary identifier. It uses IBM Plex Mono and is never truncated without an accessible full value.' },
      { title: 'Only necessary context', body: 'Detailed product contents are not shown: Puntiro labels goods that have already been picked.' },
      { title: 'Operational language', body: 'A message explains what happened, what the operator must do, and what follows. Uppercase is reserved for short utility labels.' },
    ],
  },
  'rule.feedback': {
    title: 'Feedback',
    lead: 'Every action produces an immediate, visible, meaningful response.',
    sections: [
      { title: 'State is not color alone', body: 'Status is accompanied by text, an icon, or shape. Loading, empty, invalid, failed, and unknown are documented product behavior.' },
      { title: 'Printing by place', body: 'Print progress uses a discrete 1 of N counter rather than an ornamental infinite animation.' },
      { title: 'Motion', body: '120 ms confirms hover, press, and focus; 180 ms confirms selection and completion. prefers-reduced-motion removes non-essential movement.' },
    ],
  },
  'rule.offlineAndPrinting': {
    title: 'Offline and printing',
    lead: 'Connectivity and the physical print outcome are reported independently.',
    sections: [
      { title: 'Offline semantics', body: 'Offline allowed shows the last synchronization time and remaining allowance. Offline expired blocks dependent actions and explains recovery.' },
      { title: 'Printer failure', body: 'Printer failure is a known error with a safe recovery path. The system never announces success without confirmation.' },
      { title: 'Unknown result', body: 'Unknown print result is visually, verbally, and semantically distinct from failed. Automatic reprinting is prohibited; the operator explicitly chooses what follows.' },
    ],
  },
  'rule.doAndDont': {
    title: 'Do / Don’t',
    lead: 'A short review before adding an operational state.',
    sections: [
      { title: 'Do', body: 'Show one clear next step, the full shipment number, visible status, large touch targets, and textual feedback.' },
      { title: 'Don’t', body: 'Do not add working-region scrolling, hidden gestures, nested menus, critical icon-only actions, decorative gradients, or color-only status.' },
      { title: 'Verify', body: 'Check RU and EN, keyboard, touch, reduced motion, long values, and 1280 × 800. The real display, gloves, and physical printers remain manual gates.' },
    ],
  },
  'composition.introduction': {
    title: 'Compositions',
    lead: 'This section prepares the contract for future working states without drawing product screens yet.',
    sections: [
      { title: 'Purpose', body: 'A composition will combine approved components and patterns into a complete task state after a separate design and approval stage.' },
      { title: 'Planned categories', body: 'Shipment queue, place count, printer choice, printing, success, unknown result, and archive are listed as future structure, not a finished flow.' },
      { title: 'Current boundary', body: 'There is no queue, dialog flow, archive screen, routing, or hardware integration here.' },
    ],
  },
  'composition.contract': {
    title: 'Composition contract',
    lead: 'A future composition receives prepared data and callbacks while remaining independent from I/O.',
    sections: [
      { title: 'Inputs and outputs', body: 'The application supplies data and handlers. APIs, persistence, routing, synchronization, and hardware adapters stay outside the composition.' },
      { title: 'Required invariants', body: 'A 1280 × 800 reference frame, no page or nested scroll, 64 px targets, one visible primary action, a complete long number, and accessible status are required in RU and EN.' },
      { title: 'States', body: 'Loading, empty, offline, printer failure, success, and unknown result are designed explicitly. Unknown is not treated as a normal error and never triggers automatic reprinting.' },
      { title: 'Gates', body: 'A working composition appears only after separate design, approval, automated contracts, and manual Windows, touch, glove, and hardware review.' },
    ],
  },
} satisfies Record<keyof typeof import('./content.ru').ruContent, ArticleCopy>;
