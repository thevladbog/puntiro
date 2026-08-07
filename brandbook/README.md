# Puntiro brandbook

Status: **approved**, version **1.0**, 7 August 2026.

The canonical visual brandbook is [`index.html`](./index.html). It contains the rationale, logo territories, construction, color and typography systems, label application, product UI direction, voice, and approved foundations.

## Brand core

- Name: **Puntiro** (`пунти́ро`).
- Position: an exact industrial tool for numbering and marking the real final places in a shipment.
- Promise: **Точное место. Ясная последовательность. Уверенная передача.**
- Primary logo direction: **P-Sequence**.
- Logo metaphor: a registration stem, ordered place modules, and a terminal point.
- Primary visual behavior: the orange signal identifies the current action or place; it is not decorative fill.

## Canonical assets

- [`assets/puntiro-mark.svg`](./assets/puntiro-mark.svg) — primary full-color mark.
- [`assets/puntiro-mark-mono.svg`](./assets/puntiro-mark-mono.svg) — thermal-print and one-color mark.
- [`assets/puntiro-logo-horizontal.svg`](./assets/puntiro-logo-horizontal.svg) — editable horizontal lockup. Outline the wordmark before sending to external print production.
- [`tokens.css`](./tokens.css) — CSS source of truth for implementation.
- [`tokens.json`](./tokens.json) — platform-neutral design tokens.

## Product UI foundation

- Baseline kiosk viewport: 1280 × 800, landscape; adaptive upward.
- No page scroll at the baseline viewport.
- Primary touch target: 64 px minimum, 72 px comfortable.
- Spacing grid: 8 px.
- Primary type: Onest; technical counters and codes: IBM Plex Mono.
- Labels must preserve meaning in monochrome ZPL/TSPL output.

## Usage guardrails

- Do not stretch, rotate, decorate, or add effects to the mark.
- Do not use the orange accent as a large decorative fill in operational UI.
- Do not turn the ordered-module graphic language into a fake barcode.
- Preserve at least one registration-stem width of clear space around the mark.
- Minimum mark size: 16 px in digital UI and 12 mm in print.

The next design stage is the detailed kiosk screen flow. This package defines its visual constraints but is not production UI code.
