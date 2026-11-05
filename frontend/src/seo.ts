import { useEffect } from 'react'
import { useLocation } from 'react-router'
import { t } from './i18n'

export const SITE_URL = 'https://movingmotivators.ru'

export interface PageMeta {
  title: string
  description: string
  /** Для sitemap.xml. */
  priority: number
}

/**
 * Публичные страницы для поисковиков: их пререндерим в статический HTML (scripts/prerender.mjs),
 * кладём в sitemap.xml, и только они индексируются — остальным ставим noindex.
 */
export const seoPages: Record<string, PageMeta> = {
  '/': {
    title: 'Moving Motivators онлайн — тест «Движущие мотиваторы» для команды',
    description:
      'Проведите Moving Motivators онлайн: 10 карточек мотиваторов Management 3.0, анонимный опрос команды за 7 минут, сводка и вопросы для обсуждения. До 8 человек — бесплатно.',
    priority: 1,
  },
  '/methods/moving-motivators': {
    title: 'Moving Motivators: что это за методика и как она работает',
    description:
      'Moving Motivators («Движущие мотиваторы») — практика Management 3.0 Юргена Аппело. Модель CHAMPFROGS, 10 внутренних мотиваторов, правила игры, интерпретация результатов и ответы на частые вопросы.',
    priority: 0.9,
  },
  '/moving-motivators/kartochki': {
    title: 'Карточки Moving Motivators: описание 10 мотиваторов CHAMPFROGS',
    description:
      'Подробное описание всех 10 карточек Moving Motivators: любопытство, честь, принятие, мастерство, власть, свобода, взаимосвязь, порядок, цель, статус. Как распознать мотиватор и что делать руководителю.',
    priority: 0.8,
  },
  '/moving-motivators/kak-provesti': {
    title: 'Как провести Moving Motivators: сценарий для 1-на-1 и команды',
    description:
      'Пошаговый сценарий Moving Motivators для руководителя и фасилитатора: подготовка, тайминг, вопросы для обсуждения, оценка изменений, частые ошибки, проведение онлайн и офлайн.',
    priority: 0.8,
  },
  '/moving-motivators/primenenie': {
    title: 'Где применять Moving Motivators: онбординг, 1-на-1, ретро, изменения',
    description:
      'Шесть рабочих сценариев Moving Motivators: знакомство с новой командой, онбординг, встречи 1-на-1, ретроспектива, оценка реорганизации и переезда, развитие и удержание сотрудников.',
    priority: 0.7,
  },
  '/methods/health-check': {
    title: 'Health Check команды онлайн — Squad Health Check по модели Spotify',
    description:
      'Онлайн Health Check для команды по модели Squad Health Check от Spotify: 10 категорий «светофором», тренды и история волн по кварталам.',
    priority: 0.6,
  },
  '/terms': { title: 'Публичная оферта — Движущие Мотиваторы', description: 'Публичная оферта сервиса movingmotivators.ru.', priority: 0.1 },
  '/privacy': { title: 'Политика конфиденциальности — Движущие Мотиваторы', description: 'Политика обработки персональных данных сервиса movingmotivators.ru.', priority: 0.1 },
  '/consent': { title: 'Согласие на обработку персональных данных — Движущие Мотиваторы', description: 'Согласие на обработку персональных данных пользователей сервиса movingmotivators.ru.', priority: 0.1 },
}

function setMeta(attr: 'name' | 'property', key: string, content: string | null) {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (content === null) return el?.remove()
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.content = content
}

function setCanonical(href: string | null) {
  let el = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')
  if (href === null) return el?.remove()
  if (!el) {
    el = document.createElement('link')
    el.rel = 'canonical'
    document.head.appendChild(el)
  }
  el.href = href
}

/** Синхронизирует <head> с текущим маршрутом при клиентской навигации. */
export function SeoHead() {
  const { pathname } = useLocation()
  useEffect(() => {
    const meta = seoPages[pathname]
    document.title = meta?.title ?? t.common.productName
    setMeta('name', 'description', meta?.description ?? null)
    setMeta('name', 'robots', meta ? null : 'noindex')
    setMeta('property', 'og:title', meta?.title ?? null)
    setMeta('property', 'og:description', meta?.description ?? null)
    setMeta('property', 'og:url', meta ? SITE_URL + pathname : null)
    setCanonical(meta ? SITE_URL + pathname : null)
  }, [pathname])
  return null
}
