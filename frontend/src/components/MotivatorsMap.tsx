import type { MotivatorCardInfo } from '@/api/types'
import { illustrationUrl } from '@/components/MotivatorCard'
import { fmt, t } from '@/i18n'

const LEVELS = [5, 4, 3, 2, 1]

/** Центр полосы оценки, % от низа графика: 1 звезда — 10%, 5 звёзд — 90%. */
export const ratingBottom = (rating: number) => ((rating - 0.5) / LEVELS.length) * 100

/** 4 → «4», 2.67 → «2,7» */
const shown = (rating: number) => String(Math.round(rating * 10) / 10).replace('.', ',')

/**
 * Важность и развитость на одном графике: по горизонтали — порядок важности (как в раскладке),
 * по высоте — насколько мотиватор развит в команде. Участнику показывает его ответ,
 * организатору — команду в целом (оценки могут быть дробными).
 */
export function MotivatorsMap({
  title, hint, cards, ranking, ratings,
}: {
  title: string
  hint: string
  cards: Map<string, MotivatorCardInfo>
  /** [наименее важная … самая важная] */
  ranking: string[]
  ratings: Record<string, number>
}) {
  const summary = [...ranking].reverse()
    .map((k) => fmt(t.respond.mapItem, { name: cards.get(k)?.name ?? k, n: shown(ratings[k] ?? 0) }))
    .join(', ')

  return (
    <figure className="panel p-3 sm:p-5">
      <figcaption className="mb-1 font-bold">{title}</figcaption>
      <p className="mb-4 text-sm text-zinc-500">{hint}</p>

      <div className="flex gap-1.5" role="img" aria-label={`${title}: ${summary}`}>
        {/* ось Y: звёзды */}
        <div className="flex h-72 w-6 shrink-0 flex-col text-xs text-zinc-500 sm:h-80" aria-hidden>
          {LEVELS.map((n) => <div key={n} className="flex flex-1 items-center justify-end whitespace-nowrap">{n}<span className="text-amber-500">★</span></div>)}
        </div>

        <div className="min-w-0 flex-1" aria-hidden>
          <div className="relative h-72 border-b-2 border-l-2 border-zinc-300 sm:h-80 dark:border-zinc-600">
            {/* полосы оценок */}
            <div className="absolute inset-0 flex flex-col">
              {LEVELS.map((n) => <div key={n} className="flex-1 border-t border-dashed border-zinc-200 first:border-t-0 dark:border-zinc-800" />)}
            </div>
            <div className="absolute inset-0 grid grid-cols-10">
              {ranking.map((key) => {
                const card = cards.get(key)!
                const bottom = ratingBottom(ratings[key] ?? 1)
                return (
                  <div key={key} className="relative" title={fmt(t.respond.mapItem, { name: card.name, n: shown(ratings[key] ?? 0) })}>
                    <div className="absolute bottom-0 left-1/2 w-0.5 -translate-x-1/2 opacity-60" style={{ height: `${bottom}%`, background: card.accentColor }} />
                    {/* фон маркера белый и в тёмной теме: иллюстрации рассчитаны на белую карточку */}
                    <div
                      className="absolute left-1/2 flex aspect-square w-[94%] max-w-14 -translate-x-1/2 translate-y-1/2 items-center justify-center rounded-full border-2 bg-white p-0.5 sm:p-1.5"
                      style={{ bottom: `${bottom}%`, borderColor: card.accentColor }}
                    >
                      <img src={illustrationUrl(key)} alt="" loading="lazy" className="max-h-full max-w-full object-contain" />
                    </div>
                  </div>
                )
              })}
            </div>
          </div>

          {/* подписи по оси X */}
          <div className="grid grid-cols-10 pt-1.5">
            {ranking.map((key) => (
              <div key={key} className="ml-auto mr-auto h-24 rotate-180 text-end text-xs whitespace-nowrap [writing-mode:vertical-rl]">{cards.get(key)?.name}</div>
            ))}
          </div>
          <div className="mt-1 flex justify-between text-xs font-medium text-zinc-500">
            <span>← {t.results.lessImportant}</span>
            <span>{t.results.moreImportant} →</span>
          </div>
        </div>
      </div>
    </figure>
  )
}
