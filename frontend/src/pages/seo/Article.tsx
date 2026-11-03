import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { t } from '@/i18n'
import { seoPages, SITE_URL } from '@/seo'

/** Структурированные данные schema.org; JSON-LD — не исполняемый скрипт, CSP его не блокирует. */
export function JsonLd({ data }: { data: object }) {
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify({ '@context': 'https://schema.org', ...data }).replace(/</g, '\\u003c') }}
    />
  )
}

export function Breadcrumbs({ items }: { items: { path: string; name: string }[] }) {
  const all = [{ path: '/', name: 'Главная' }, ...items]
  return (
    <>
      <nav aria-label="Хлебные крошки" className="mb-4 text-sm text-zinc-500">
        {all.map((it, i) => (
          <span key={it.path}>
            {i > 0 && ' / '}
            {i < all.length - 1 ? <Link to={it.path} className="hover:underline">{it.name}</Link> : <span>{it.name}</span>}
          </span>
        ))}
      </nav>
      <JsonLd
        data={{
          '@type': 'BreadcrumbList',
          itemListElement: all.map((it, i) => ({ '@type': 'ListItem', position: i + 1, name: it.name, item: SITE_URL + it.path })),
        }}
      />
    </>
  )
}

export interface FaqItem {
  q: string
  a: string
}

export function Faq({ title = 'Частые вопросы', items }: { title?: string; items: FaqItem[] }) {
  return (
    <section className="space-y-4">
      <h2 className="pt-2 text-2xl font-bold">{title}</h2>
      {items.map((it) => (
        <div key={it.q}>
          <h3 className="mb-1 text-lg font-bold">{it.q}</h3>
          <p className="text-zinc-700 dark:text-zinc-300">{it.a}</p>
        </div>
      ))}
      <JsonLd
        data={{
          '@type': 'FAQPage',
          mainEntity: items.map((it) => ({ '@type': 'Question', name: it.q, acceptedAnswer: { '@type': 'Answer', text: it.a } })),
        }}
      />
    </section>
  )
}

/** Article + Organization для поисковой выдачи. */
export function ArticleLd({ path, headline }: { path: string; headline: string }) {
  return (
    <JsonLd
      data={{
        '@type': 'Article',
        headline,
        description: seoPages[path]?.description,
        inLanguage: 'ru',
        mainEntityOfPage: SITE_URL + path,
        publisher: { '@type': 'Organization', name: 'Движущие Мотиваторы', url: SITE_URL },
      }}
    />
  )
}

export function Cta({ title = 'Проведите Moving Motivators онлайн', children }: { title?: string; children?: ReactNode }) {
  return (
    <div className="panel my-8 border-brand/30 bg-brand-50 dark:bg-zinc-900">
      <h2 className="mb-2 text-xl font-bold">{title}</h2>
      <p className="mb-4 text-zinc-700 dark:text-zinc-300">
        {children ??
          'Отправьте команде ссылку — каждый разложит 10 карточек с телефона за 5 минут, без регистрации. Вы получите сводку команды, тепловую карту и вопросы для обсуждения. До 8 участников — бесплатно.'}
      </p>
      <Link to="/login" className="btn-primary">{t.landing.cta}</Link>
    </div>
  )
}

const related = [
  { path: '/methods/moving-motivators', name: 'Что такое Moving Motivators' },
  { path: '/moving-motivators/kartochki', name: '10 карточек мотиваторов' },
  { path: '/moving-motivators/kak-provesti', name: 'Как провести Moving Motivators' },
  { path: '/moving-motivators/primenenie', name: 'Где применять Moving Motivators' },
]

export function ReadNext({ current }: { current: string }) {
  return (
    <nav aria-label="Читайте также" className="space-y-2">
      <h2 className="text-xl font-bold">Читайте также</h2>
      <ul className="list-disc space-y-1 pl-6">
        {related.filter((r) => r.path !== current).map((r) => (
          <li key={r.path}><Link to={r.path} className="underline">{r.name}</Link></li>
        ))}
      </ul>
    </nav>
  )
}
