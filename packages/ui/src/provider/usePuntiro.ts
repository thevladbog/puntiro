import { useContext } from 'react';
import { PuntiroContext } from './PuntiroProvider';
import type { PuntiroContextValue } from './types';

export function usePuntiro(): PuntiroContextValue {
  const context = useContext(PuntiroContext);

  if (context === null) {
    throw new Error('PuntiroProvider is missing');
  }

  return context;
}
