import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/api/client'
import { useCatalog } from '@/api/hooks'
import type { MotivatorsComparison, SurveyListItem } from '@/api/types'
import { ErrorBox, Spinner } from '@/components/ui'
import { t } from '@/i18n'
import { cardMap, Chip, signed } from './common'

/** Сдвиг среднего ранга карточек между двумя опросами ММ одной команды. */
export default function MotivatorsCompare({ surveys }: { surveys: SurveyListItem[] }) {
  // список отсортирован от новых к старым
  const [a, setA] = useState(surveys[1].id)
  const [b, setB] = useState(surveys[0].id)
  const { data: catalog } = useCatalog()
  const { data, error, isFetching } = useQuery({
    queryKey: ['compare', a, b],
    queryFn: () => api.get<MotivatorsComparison>(`/surveys/compare?a=${a}&b=${b}`),
    enabled: a !== b,
  })
  const cards = catalog ? cardMap(catalog) : new Map()

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap gap-3">
        {[
          { label: t.teams.compareA, value: a, set: setA },
          { label: t.teams.compareB, value: b, set: setB },
        ].map((f) => (
          <label key={f.label} className="min-w-48 flex-1">
            <span className="label">{f.label}</span>
            <select className="input" value={f.value} onChange={(e) => f.set(e.target.value)}>
              {surveys.map((s) => <option key={s.id} value={s.id}>{s.title}</option>)}
            </select>
          </label>
        ))}
      </div>
      {error && <ErrorBox error={error} />}
      {isFetching && <Spinner />}
      {data && !isFetching && (
        <>
          <div className="panel space-y-1.5">
            <div className="mb-2 grid grid-cols-[8.5rem_1fr_1fr_4rem] gap-3 text-xs text-zinc-500 sm:grid-cols-[11rem_1fr_1fr_4rem]">
              <span />
              <span>{data.a.title}</span>
              <span>{data.b.title}</span>
              <span className="text-right">{t.results.shift}</span>
            </div>
            {[...data.cards].sort((x, y) => y.avgB - x.avgB).map((c) => (
              <div key={c.key} className="grid grid-cols-[8.5rem_1fr_1fr_4rem] items-center gap-3 text-sm sm:grid-cols-[11rem_1fr_1fr_4rem]">
                <Chip card={cards.get(c.key)} />
                <span className="text-zinc-500">{c.avgA.toFixed(1)}</span>
                <span className="font-medium">{c.avgB.toFixed(1)}</span>
                <span className={`text-right font-mono font-bold ${c.delta > 0 ? 'text-green-700 dark:text-green-400' : c.delta < 0 ? 'text-red-700 dark:text-red-400' : 'text-zinc-400'}`}>
                  {signed(c.delta)}
                </span>
              </div>
            ))}
          </div>
          {data.participants && data.participants.length > 0 && (
            <div className="overflow-x-auto">
              <table className="min-w-full border-separate border-spacing-0.5 text-sm">
                <thead>
                  <tr>
                    <th />
                    {data.cards.map((c) => (
                      <th key={c.key} className="h-24 align-bottom">
                        <div className="mx-auto w-6 [writing-mode:vertical-rl] rotate-180 text-left text-xs font-normal whitespace-nowrap">{cards.get(c.key)?.name}</div>
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.participants.map((p) => (
                    <tr key={p.name}>
                      <th className="pr-3 text-left font-medium whitespace-nowrap">{p.name}</th>
                      {data.cards.map((c) => {
                        const v = p.shift[c.key] ?? 0
                        return (
                          <td key={c.key} className={`h-8 min-w-8 rounded text-center text-xs font-bold ${v > 0 ? 'bg-green-100 text-green-800' : v < 0 ? 'bg-red-100 text-red-800' : 'text-zinc-400'}`}>
                            {v > 0 ? `+${v}` : v}
                          </td>
                        )
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  )
}
