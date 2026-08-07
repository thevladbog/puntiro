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

const iconRegistry = {
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
} as const;

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

export function getIcon(name: IconName) {
  return iconRegistry[name];
}
