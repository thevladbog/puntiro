import { describe, expect, it } from 'vitest';
import { defineDocumentation } from './defineDocumentation';

const complete = {
  overview: 'Overview', anatomy: ['Root'], variants: ['Default'], states: ['Ready'],
  behavior: ['Press'], content: ['Use verbs'], accessibility: ['Named'], usage: ['Example'],
  do: ['Be clear'], dont: ['Hide actions'], changelog: ['0.1.0 Initial'],
};

describe('defineDocumentation', () => {
  it('requires complete RU and EN documentation', () => {
    expect(() => defineDocumentation({ ru: complete, en: { ...complete, do: [] } }))
      .toThrow('en.do must contain at least one entry');
  });
});
