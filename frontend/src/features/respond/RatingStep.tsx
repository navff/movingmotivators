import type { MotivatorCardInfo } from '@/api/types'
import { MotivatorCard } from '@/components/MotivatorCard'
import { fmt, t } from '@/i18n'

/** Строка из n закрашенных звёзд из 5. */
export function Stars({ value, className = '' }: { value: number; className?: string }) {
  const total = 5
  return (
    <span className={`whitespace-nowrap tracking-tight ${className}`} aria-hidden>
      <span className="text-amber-500">{'★'.repeat(value)}</span>
      <span className="text-zinc-300 dark:text-zinc-600">{'★'.repeat(total - value)}</span>
    </span>
  )
}

/**
 * Обязательный шаг: насколько мотиватор развит в команде, 1–5 звёзд.
 * У каждой оценки своё описание — чтобы все понимали шкалу одинаково.
 */
export function RatingStep({
  card, index, total, value, onChange, comment, onCommentChange,
}: {
  card: MotivatorCardInfo
  index: number
  total: number
  value?: number
  onChange: (v: number) => void
  comment: string
  onCommentChange: (v: string) => void
}) {
  return (
    <div>
      <div className="mb-1 text-sm font-medium text-zinc-500">{fmt(t.respond.rateStep, { i: index + 1, n: total })}</div>
      <h2 className="mb-2 text-xl font-bold">{fmt(t.respond.rateQuestion, { name: card.name })}</h2>
      <p className="mb-4 text-zinc-600 dark:text-zinc-300">{t.respond.rateHint}</p>
      <MotivatorCard card={card} mode="compact" className="mb-4" />
      <div className="space-y-2" role="radiogroup" aria-label={fmt(t.respond.rateQuestion, { name: card.name })}>
        {card.levels!.map((text, i) => {
          const stars = i + 1
          const on = value === stars
          return (
            <button
              key={stars}
              type="button"
              role="radio"
              aria-checked={on}
              aria-label={`${fmt(t.respond.rateStars, { n: stars })}: ${text}`}
              onClick={() => onChange(stars)}
              className={`flex w-full flex-col gap-1 rounded-lg border-2 p-3 text-left transition sm:flex-row sm:items-center sm:gap-4 ${
                on ? 'border-brand bg-brand-50 dark:bg-zinc-800' : 'border-zinc-200 bg-white hover:border-zinc-400 dark:border-zinc-700 dark:bg-zinc-900'
              }`}
            >
              <Stars value={stars} className="shrink-0 text-xl" />
              <span className="text-sm leading-snug">{text}</span>
            </button>
          )
        })}
      </div>
      {/* не вариант ответа: отделено линией, подпись над полем, пунктир и прозрачный фон */}
      <div className="mt-5 border-t border-zinc-200 pt-4 dark:border-zinc-800">
        <label className="label" htmlFor="rating-comment">
          {t.respond.rateCommentLabel} <span className="font-normal text-zinc-400">({t.common.optional})</span>
        </label>
        <textarea
          id="rating-comment"
          className="input border-dashed bg-transparent! text-sm"
          rows={2}
          maxLength={500}
          placeholder={t.respond.rateCommentPlaceholder}
          value={comment}
          onChange={(e) => onCommentChange(e.target.value)}
        />
      </div>
    </div>
  )
}
