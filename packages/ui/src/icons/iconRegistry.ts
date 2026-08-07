import {
  Archive,
  Cable,
  Check,
  ChevronDown,
  CircleAlert,
  Minus,
  Plus,
  Printer,
  RefreshCw,
  Search,
  TriangleAlert,
  X
} from 'lucide-react';
import type { ComponentType, SVGProps } from 'react';

type PuntiroGlyphProps = Pick<SVGProps<SVGSVGElement>, 'aria-hidden' | 'className' | 'focusable' | 'strokeWidth'>;
type PuntiroGlyph = ComponentType<PuntiroGlyphProps>;

export const iconNames = [
  'archive',
  'check',
  'chevronDown',
  'close',
  'connection',
  'error',
  'minus',
  'plus',
  'printer',
  'refresh',
  'search',
  'warning'
] as const;

export type IconName = typeof iconNames[number];

const iconRegistry: Record<IconName, PuntiroGlyph> = {
  archive: Archive,
  check: Check,
  chevronDown: ChevronDown,
  close: X,
  connection: Cable,
  error: CircleAlert,
  minus: Minus,
  plus: Plus,
  printer: Printer,
  refresh: RefreshCw,
  search: Search,
  warning: TriangleAlert
};

export function getIcon(name: IconName): PuntiroGlyph {
  return iconRegistry[name];
}
