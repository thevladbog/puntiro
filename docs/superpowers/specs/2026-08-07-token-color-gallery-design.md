# Puntiro token color gallery

## Problem

The Storybook token catalog currently presents color tokens as names and hex
values only. It is accurate as a reference, but readers cannot scan the Puntiro
palette or understand the visual relationship between semantic roles without
mentally translating every code.

## Approved direction

Use a hybrid color catalog:

1. a prominent gallery with five key semantic roles and one grouped status
   family card;
2. the complete compact token table with a color swatch in every color row.

The gallery explains the visual language first, while the table remains the
precise and exhaustive engineering reference.

## Key gallery

The gallery contains:

- `semantic.color.canvas.default`;
- `semantic.color.surface.default`;
- `semantic.color.text.primary`;
- `semantic.color.action.primary`;
- `semantic.color.selected.background`;
- one grouped status card containing success, warning, and danger.

Each ordinary gallery card shows a large color field, the semantic role, the
full token path, and its resolved value. The status card shows three equal
color fields with their individual labels and values.

The gallery is ordered from environment and content roles to interaction and
feedback roles. It is not configurable by Storybook controls in the first
version.

## Complete catalog

The existing table remains exhaustive and keeps its `Token` and `Value`
columns. Color rows add a leading `Preview` column containing a compact swatch.
Non-color token groups keep the existing two-column layout and behavior.

The table and gallery must read values from the generated token artifact used
by `TokenGallery`. Neither component code nor documentation copies hex values.
Changing a source token and rebuilding generated artifacts must update both
representations.

## Visual behavior

- Gallery cards use the existing Puntiro Docs surfaces, borders, radius,
  typography, and spacing tokens.
- Light swatches remain visible through a semantic border.
- Dark swatches receive a subtle light inset boundary in addition to the outer
  border.
- The gallery uses three columns when space permits, two at intermediate
  widths, and one on narrow viewports.
- The complete page has no horizontal overflow when Storybook runs at the
  minimum supported 1280x800 viewport; it also adapts to wider viewports.
- No gradient, generated illustration, or decorative color naming is added.

## Accessibility

Color is supplementary information, never the only identifier. Every swatch is
paired with its token name and resolved value. Decorative swatch elements are
hidden from the accessibility tree; the adjacent text remains the accessible
source of truth.

Borders must keep very light colors distinguishable from the Docs canvas and
very dark colors distinguishable from their card boundary. The layout must not
require hover, pointer precision, or horizontal scrolling to inspect tokens.

## Component boundary

`TokenGallery` continues to own token collection and group selection. The
color-only visual treatment is split into small internal renderers for the key
gallery, a color row, and the grouped status card. These renderers receive
resolved token entries and do not import or duplicate token files themselves.

The public Storybook documentation API does not change. Product components,
`@puntiro/ui` exports, token source values, and generated token formats remain
out of scope.

## Regression contract

Automated coverage must prove that:

- the five approved individual roles and the grouped status family are present;
- the status card exposes success, warning, and danger individually;
- displayed values come from the current generated token artifact;
- every color table row has a swatch and textual name/value;
- non-color galleries retain their existing table structure;
- swatches are decorative rather than unlabeled interactive elements;
- the rendered color catalog has no horizontal overflow in Storybook at
  1280x800.

The existing component screenshot baselines must remain unchanged because the
change is limited to documentation content.
