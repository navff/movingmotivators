import { lazy, Suspense, useState } from 'react'
import type { Catalog, HealthCheckResult, Survey } from '@/api/types'
import { Spinner, Tabs } from '@/components/ui'
import { trafficColor, trendArrow } from '@/lib/colors'
import { t } from '@/i18n'
import { Comments, Prompts } from './common'

const HealthCheckHistory = lazy(() => import('./HealthCheckHistory'))

type Tab = 'traffic' | 'history' | 'comments' | 'discussion'

export function HealthCheckResults({ survey, result, catalog }: { survey: Survey; result: HealthCheckResult; catalog: Catalog }) {
  const [tab, setTab] = useState<Tab>('traffic')
  const cats = new Map(catalog.healthCheckCategories.map((c) => [c.key, c]))
  const commentCount = result.comments.length + result.categories.reduce((s, c) => s + c.comments.length, 0)

  return (
    <>
      <Tabs
        tabs={[
          { id: 'traffic', label: t.results.tabTraffic },
          { id: 'history', label: t.results.tabHistory },
          { id: 'comments', label: `${t.results.tabComments} (${commentCount})` },
          { id: 'discussion', label: t.results.tabDiscussion },
        ]}
        value={tab}
        onChange={setTab}
      />

      {tab === 'traffic' && (
        <div className="space-y-8">
          {result.focus.length > 0 && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-900 dark:bg-amber-950/40">
              <div className="mb-1 text-xs font-bold uppercase text-amber-800 dark:text-amber-300">{t.results.focus}</div>
              <div className="font-medium">{result.focus.map((k) => cats.get(k)?.name).join(', ')}</div>
            </div>
          )}
          {result.overallAvg != null && (
            <p className="text-sm text-zinc-600 dark:text-zinc-400">
              {t.results.overallAvg}: <b className="font-slab text-lg">{result.overallAvg.toFixed(2)}</b> / 3
            </p>
          )}

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {result.categories.map((c) => {
              const info = cats.get(c.key)!
              const n = Math.max(1, c.green + c.yellow + c.red)
              return (
                <div key={c.key} className="panel">
                  <div className="mb-3 flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <span className="h-8 w-8 shrink-0 rounded-full shadow-inner" style={{ background: trafficColor(c.color) }} aria-hidden />
                      <div>
                        <div className="font-bold">{info.name}</div>
                        <div className="text-xs text-zinc-500">
                          {c.color ? t.results[c.color] : '—'}
                          {c.avgScore != null && ` · ${c.avgScore.toFixed(2)}`}
                        </div>
                      </div>
                    </div>
                    <span className="text-2xl" title={t.results.hcTrend} aria-label={t.results.hcTrend}>{trendArrow(c.trend)}</span>
                  </div>
                  <div className="flex h-3 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                    <div style={{ width: `${(c.green / n) * 100}%`, background: trafficColor('green') }} />
                    <div style={{ width: `${(c.yellow / n) * 100}%`, background: trafficColor('yellow') }} />
                    <div style={{ width: `${(c.red / n) * 100}%`, background: trafficColor('red') }} />
                  </div>
                  <div className="mt-2 flex justify-between text-xs text-zinc-500">
                    <span>🟢 {c.green} · 🟡 {c.yellow} · 🔴 {c.red}</span>
                    <span>↑{c.trendUp} →{c.trendSame} ↓{c.trendDown}</span>
                  </div>
                </div>
              )
            })}
          </div>

          {result.participants && (
            <div className="overflow-x-auto">
              <table className="min-w-full border-separate border-spacing-0.5 text-sm">
                <thead>
                  <tr>
                    <th className="text-left text-xs font-normal text-zinc-500">{t.results.participant}</th>
                    {result.categories.map((c) => (
                      <th key={c.key} className="px-1 text-center text-xs font-normal">{cats.get(c.key)?.name}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.participants.map((p, i) => (
                    <tr key={i}>
                      <th className="pr-3 text-left font-medium whitespace-nowrap">{p.name}</th>
                      {result.categories.map((c) => {
                        const a = p.answers[c.key]
                        const color = a?.score === 3 ? 'green' : a?.score === 2 ? 'yellow' : 'red'
                        return (
                          <td key={c.key} className="h-9 rounded text-center font-bold text-zinc-900" style={{ background: trafficColor(color) }}
                            title={a?.comment ?? undefined}>
                            {trendArrow(a?.trend)}
                          </td>
                        )
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {tab === 'history' && (
        <Suspense fallback={<Spinner />}>
          <HealthCheckHistory teamId={survey.teamId} />
        </Suspense>
      )}

      {tab === 'comments' && (
        <div className="space-y-8">
          {result.categories.filter((c) => c.comments.length > 0).map((c) => (
            <section key={c.key}>
              <h3 className="mb-3 font-bold">{cats.get(c.key)?.name}</h3>
              <Comments items={c.comments} anonymous={survey.isAnonymous} />
            </section>
          ))}
          {result.comments.length > 0 && (
            <section>
              <h3 className="mb-3 font-bold">{t.results.generalComments}</h3>
              <Comments items={result.comments} anonymous={survey.isAnonymous} />
            </section>
          )}
          {commentCount === 0 && <p className="text-zinc-500">{t.results.noComments}</p>}
        </div>
      )}

      {tab === 'discussion' && <Prompts prompts={result.prompts} />}
    </>
  )
}
