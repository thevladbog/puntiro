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
} satisfies Record<keyof typeof import('./content.ru').ruContent, ArticleCopy>;
