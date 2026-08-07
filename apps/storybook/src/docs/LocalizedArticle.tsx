import { usePuntiro } from '@puntiro/ui';
import type { ReactNode } from 'react';
import { enContent } from './content.en';
import { ruContent, type ArticleId } from './content.ru';
import './start.css';

export function LocalizedArticle({ id, children }: { id: ArticleId; children?: ReactNode }) {
  const { locale } = usePuntiro();
  const article = (locale === 'en' ? enContent : ruContent)[id];

  return (
    <article className="puntiro-article">
      <header className="puntiro-article__header"><h1>{article.title}</h1><p>{article.lead}</p></header>
      {article.sections.map((section) => <section key={section.title}><h2>{section.title}</h2><p>{section.body}</p></section>)}
      {children}
    </article>
  );
}
