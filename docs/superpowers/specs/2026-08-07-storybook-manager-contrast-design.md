# Storybook manager contrast fix

## Problem

The Puntiro Storybook manager declares `base: 'light'` while using the dark
`#171914` application background. Storybook therefore renders some manager
labels with the dark `textColor` on the same dark surface. The effective
contrast is `1.00:1`, so sidebar story names and parts of the toolbar are not
readable. The selected story currently uses light text on the orange accent,
which reaches only `3.06:1` and also misses WCAG AA for normal text.

The component preview and Docs content are separate surfaces and are already
intended to remain light.

## Approved direction

Use a coherent dark theme for the Storybook manager chrome and keep the preview
canvas and Docs theme light.

Manager color roles:

- manager base, sidebar, toolbar, and addon panel: dark;
- primary manager text: `#F2F0E8` on `#171914` (`15.52:1`);
- secondary manager text: `#C8C8BE` on `#171914` (`10.51:1`);
- selected/accent roles: `#171914` on `#FF5A1F` (`5.68:1`) where the
  supported theme role is used;
- dark controls and inputs: `#292C26` with `#FFFDF6` text;
- component preview canvas: light `#FFFDF6` / `#F2F0E8` surfaces;
- Docs theme: retain its independent light palette.

All normal manager text must meet at least WCAG AA `4.5:1`. UI boundaries and
focus indicators must meet at least `3:1` against adjacent surfaces.

### Approved Storybook-native selected-sidebar exception

For the Storybook dark manager's selected sidebar story, accept the supported
native rendering rather than overriding manager internals: Storybook derives
`rgb(194, 51, 0)` from Puntiro `#FF5A1F` and renders light selected text
(`rgb(255, 255, 255)`). This explicit selected-sidebar pair must remain at or
above `4.5:1` in the rendered browser contract.

Do not add manager-internal CSS to force the generic selected/accent role onto
this state. The unselected sidebar state remains `rgb(242, 240, 232)` on
`rgb(23, 25, 20)` and must also be asserted before contrast is measured.

## Implementation boundary

Refactor the current shared Storybook theme configuration so manager semantic
roles do not inherit light-theme text roles. Use Storybook's supported theming
API rather than CSS selectors into manager internals. The fix must cover the
sidebar, top toolbar, addon tabs and controls, search/input surfaces, hover,
selected, and focus states as one coherent theme.

Do not change Puntiro component tokens, component rendering, Docs typography,
story content, public `@puntiro/ui` APIs, or product screens.

## Regression contract

Add a focused automated contrast contract before changing production theme
values. It must fail against the current mixed theme and then verify the dark
manager roles and their contrast ratios. Add a real Playwright manager check for
representative visible states when stable Storybook roles are available:

- an unselected sidebar story;
- the selected sidebar story;
- top/addon toolbar text;
- a Controls field and its input text.

The browser check must assert the explicit computed foreground/background pair
for selected and unselected sidebar stories before calculating contrast, rather
than rely only on a screenshot. Existing component visual baselines must remain
unchanged because the preview canvas is not being redesigned.

## Verification

Run the focused RED/GREEN regression, manager browser check, Storybook static
build, and the complete `CI=true corepack pnpm check`. Inspect the live manager
at `http://localhost:6006` after hot reload. Manual Windows/high-contrast and
screen-reader reviews remain separate manual gates.
