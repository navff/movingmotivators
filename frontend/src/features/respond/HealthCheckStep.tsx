import type { HealthCheckAnswer, HealthCheckCategoryInfo } from '@/api/types'
import { t } from '@/i18n'

const scores = [
  { v: 3, emoji: '🟢', label: t.respond.awesome, on: 'border-green-600 bg-green-50 dark:bg-green-950' },
  { v: 2, emoji: '🟡', label: t.respond.soso, on: 'border-amber-500 bg-amber-50 dark:bg-amber-950' },
  { v: 1, emoji: '🔴', label: t.respond.awful, on: 'border-red-600 bg-red-50 dark:bg-red-950' },
]
const trends = [
  { v: 1, label: `↑ ${t.results.trendUp}` },
  { v: 0, label: `→ ${t.results.trendSame}` },
  { v: -1, label: `↓ ${t.results.trendDown}` },
]

export type HcDraft = Partial<HealthCheckAnswer>

export function HealthCheckCategoryForm({
  category, value, onChange,
}: {
  category: HealthCheckCategoryInfo
  value: HcDraft
  onChange: (v: HcDraft) => void
}) {
  return (
    <div className="panel space-y-4">
      <h2 className="text-xl font-bold">{category.name}</h2>
      <div className="grid gap-2 text-sm sm:grid-cols-2">
        <p className="rounded-lg bg-green-50 p-3 text-green-900 dark:bg-green-950/50 dark:text-green-200">🟢 {category.awesome}</p>
        <p className="rounded-lg bg-red-50 p-3 text-red-900 dark:bg-red-950/50 dark:text-red-200">🔴 {category.awful}</p>
      </div>
      <fieldset>
        <legend className="label">{t.respond.hcScore}</legend>
        <div className="grid grid-cols-3 gap-2">
          {scores.map((s) => (
            <button
              key={s.v}
              type="button"
              aria-pressed={value.score === s.v}
              onClick={() => onChange({ ...value, score: s.v })}
              className={`flex min-h-16 flex-col items-center justify-center rounded-lg border-2 p-2 text-sm ${value.score === s.v ? s.on : 'border-zinc-200 dark:border-zinc-700'}`}
            >
              <span className="text-2xl" aria-hidden>{s.emoji}</span>
              {s.label}
            </button>
          ))}
        </div>
      </fieldset>
      <fieldset>
        <legend className="label">{t.respond.hcTrend}</legend>
        <div className="grid grid-cols-3 gap-2">
          {trends.map((tr) => (
            <button
              key={tr.v}
              type="button"
              aria-pressed={value.trend === tr.v}
              onClick={() => onChange({ ...value, trend: tr.v })}
              className={`min-h-11 rounded-lg border-2 px-2 text-sm ${value.trend === tr.v ? 'border-brand bg-brand-50 font-medium dark:bg-zinc-800' : 'border-zinc-200 dark:border-zinc-700'}`}
            >
              {tr.label}
            </button>
          ))}
        </div>
      </fieldset>
      <textarea
        className="input text-sm"
        rows={2}
        maxLength={500}
        placeholder={t.respond.hcCommentPlaceholder}
        value={value.comment ?? ''}
        onChange={(e) => onChange({ ...value, comment: e.target.value })}
        aria-label={`${category.name}: комментарий`}
      />
    </div>
  )
}

export const isComplete = (v?: HcDraft) => v?.score != null && v?.trend != null
