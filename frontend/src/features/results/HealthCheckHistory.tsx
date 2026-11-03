import { useQuery } from '@tanstack/react-query'
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '@/api/client'
import { useCatalog } from '@/api/hooks'
import type { HealthCheckHistory as History } from '@/api/types'
import { Empty, ErrorBox, Spinner } from '@/components/ui'
import { trafficColor, trendArrow } from '@/lib/colors'
import { formatDate, t } from '@/i18n'

/** Сетка «категория × волна» (как в модели Spotify) + график среднего балла. */
export default function HealthCheckHistory({ teamId }: { teamId: string }) {
  const { data: catalog } = useCatalog()
  const { data, error } = useQuery({
    queryKey: ['teams', teamId, 'history'],
    queryFn: () => api.get<History>(`/teams/${teamId}/history?type=health_check`),
  })
  if (error) return <ErrorBox error={error} />
  if (!data || !catalog) return <Spinner />
  if (data.waves.length === 0) return <Empty>{t.teams.historyEmpty}</Empty>

  const names = new Map(catalog.healthCheckCategories.map((c) => [c.key, c.name]))
  const chart = data.waves.map((w) => ({ name: w.title, avg: w.overallAvg ?? null }))

  return (
    <div className="space-y-8">
      <div className="overflow-x-auto">
        <table className="min-w-full border-separate border-spacing-1 text-sm">
          <thead>
            <tr>
              <th />
              {data.waves.map((w) => (
                <th key={w.surveyId} className="min-w-20 px-1 text-center text-xs font-medium">
                  <div>{w.title}</div>
                  <div className="font-normal text-zinc-500">{formatDate(w.closedAt)}</div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.categoryKeys.map((k) => (
              <tr key={k}>
                <th className="pr-3 text-left font-normal whitespace-nowrap">{names.get(k) ?? k}</th>
                {data.waves.map((w) => {
                  const cell = w.categories[k]
                  return (
                    <td key={w.surveyId} className="h-9 rounded-full text-center font-bold text-zinc-900"
                      style={{ background: cell ? trafficColor(cell.color) : 'transparent' }}
                      title={cell?.avgScore != null ? cell.avgScore.toFixed(2) : undefined}>
                      {cell ? trendArrow(cell.trend) : ''}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.waves.length > 1 && (
        <div className="panel h-64">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chart} margin={{ top: 10, right: 16, bottom: 0, left: -16 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#a1a1aa55" />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} />
              <YAxis domain={[1, 3]} ticks={[1, 1.5, 2, 2.5, 3]} tick={{ fontSize: 12 }} />
              <Tooltip formatter={(v) => [Number(v).toFixed(2), t.results.overallAvg]} />
              <Line type="monotone" dataKey="avg" stroke="#493082" strokeWidth={3} dot={{ r: 5 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  )
}
