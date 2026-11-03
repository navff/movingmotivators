import type { MotivatorCardInfo, SurveyChange } from '@/api/types'
import { MotivatorCard } from '@/components/MotivatorCard'
import { fmt, t } from '@/i18n'

const options = [
  { v: 1, label: '↑', title: t.results.better, on: 'bg-green-600 text-white border-green-600' },
  { v: 0, label: '—', title: t.results.same, on: 'bg-zinc-600 text-white border-zinc-600' },
  { v: -1, label: '↓', title: t.results.worse, on: 'bg-red-600 text-white border-red-600' },
]

/** Карточки — в порядке собственного ранжирования (самые важные сверху), порядок не меняется. */
export function ImpactStep({
  change, index, total, cards, ranking, marks, onChange,
}: {
  change: SurveyChange
  index: number
  total: number
  cards: Map<string, MotivatorCardInfo>
  ranking: string[]
  marks: Record<string, number>
  onChange: (m: Record<string, number>) => void
}) {
  const ordered = [...ranking].reverse()
  return (
    <div>
      <div className="mb-1 text-sm font-medium text-zinc-500">{fmt(t.respond.impactStep, { i: index + 1, n: total })}</div>
      <h2 className="mb-2 text-xl font-bold">«{change.text}»</h2>
      <p className="mb-4 text-zinc-600 dark:text-zinc-300">{t.respond.impactText}</p>
      <div className="mb-3 flex justify-end gap-3 text-xs text-zinc-500">
        {options.map((o) => <span key={o.v}>{o.label} {o.title}</span>)}
      </div>
      <div className="space-y-2">
        {ordered.map((key) => {
          const value = marks[key] ?? 0
          const card = cards.get(key)!
          return (
            <MotivatorCard
              key={key}
              card={card}
              mode="compact"
              actions={
                <div className="flex gap-1" role="radiogroup" aria-label={card.name}>
                  {options.map((o) => (
                    <button
                      key={o.v}
                      type="button"
                      role="radio"
                      aria-checked={value === o.v}
                      aria-label={`${card.name}: ${o.title}`}
                      title={o.title}
                      onClick={() => onChange({ ...marks, [key]: o.v })}
                      className={`h-10 w-10 rounded-lg border text-lg font-bold transition ${value === o.v ? o.on : 'border-zinc-300 bg-white text-zinc-500'}`}
                    >
                      {o.label}
                    </button>
                  ))}
                </div>
              }
            />
          )
        })}
      </div>
    </div>
  )
}
