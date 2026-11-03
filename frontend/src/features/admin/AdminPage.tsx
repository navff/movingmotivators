import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '@/api/client'
import type { AdminFunnel, AdminStats, AdminUser } from '@/api/types'
import { Page } from '@/components/Layout'
import { ErrorBox, PageTitle, Spinner } from '@/components/ui'
import { formatDate, t } from '@/i18n'

const iso = (d: Date) => d.toISOString().slice(0, 10)
const pct = (v: number) => `${(v * 100).toFixed(1)}%`
const methodNames: Record<string, string> = { card: t.checkout.card, sbp: t.checkout.sbp, sberpay: t.checkout.sberpay }

function Stat({ label, value, accent }: { label: string; value: string | number; accent?: boolean }) {
  return (
    <div className={`panel ${accent ? 'border-brand' : ''}`}>
      <div className="text-sm text-zinc-500">{label}</div>
      <div className="mt-1 font-slab text-2xl font-bold">{value}</div>
    </div>
  )
}

export default function AdminPage() {
  const [from, setFrom] = useState(() => iso(new Date(Date.now() - 29 * 86400_000)))
  const [to, setTo] = useState(() => iso(new Date()))
  const [q, setQ] = useState('')
  const range = `from=${from}&to=${to}`

  const stats = useQuery({ queryKey: ['admin', 'stats', from, to], queryFn: () => api.get<AdminStats>(`/admin/stats?${range}`) })
  const funnel = useQuery({ queryKey: ['admin', 'funnel', from, to], queryFn: () => api.get<AdminFunnel>(`/admin/funnel?${range}`) })
  const users = useQuery({ queryKey: ['admin', 'users', q], queryFn: () => api.get<AdminUser[]>(`/admin/users?q=${encodeURIComponent(q)}`) })

  return (
    <Page wide>
      <PageTitle>{t.admin.title}</PageTitle>

      <div className="mb-8 flex flex-wrap items-end gap-3">
        <label>
          <span className="label">{t.admin.from}</span>
          <input type="date" className="input" value={from} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label>
          <span className="label">{t.admin.to}</span>
          <input type="date" className="input" value={to} onChange={(e) => setTo(e.target.value)} />
        </label>
      </div>

      <section className="mb-10">
        <h2 className="mb-4 text-xl font-bold">{t.admin.funnel}</h2>
        {funnel.error && <ErrorBox error={funnel.error} />}
        {funnel.isLoading && <Spinner />}
        {funnel.data && (
          <>
            <div className="mb-4 grid grid-cols-2 gap-3 md:grid-cols-4">
              <Stat label={t.admin.free} value={funnel.data.freeSurveys} />
              <Stat label={t.admin.paidRequired} value={funnel.data.paidRequiredSurveys} />
              <Stat label={t.admin.checkoutViewed} value={funnel.data.checkoutViewed} />
              <Stat label={t.admin.methodSelected} value={funnel.data.paymentMethodSelected} />
              <Stat label={t.admin.paymentClicked} value={funnel.data.paymentClicked} accent />
              <Stat label={t.admin.abandoned} value={funnel.data.checkoutAbandoned} />
              <Stat label={t.admin.conversionDraft} value={pct(funnel.data.conversionFromDraft)} accent />
              <Stat label={t.admin.conversionCheckout} value={pct(funnel.data.conversionFromCheckout)} />
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="panel">
                <h3 className="mb-2 font-bold">{t.admin.byMethod}</h3>
                {funnel.data.byMethod.length === 0 ? <p className="text-sm text-zinc-500">—</p> : (
                  <ul className="space-y-1 text-sm">
                    {funnel.data.byMethod.map((m) => (
                      <li key={m.key} className="flex justify-between"><span>{methodNames[m.key] ?? m.key}</span><b>{m.count}</b></li>
                    ))}
                  </ul>
                )}
              </div>
              <div className="panel">
                <h3 className="mb-2 font-bold">{t.admin.bySize}</h3>
                <ul className="space-y-1 text-sm">
                  {funnel.data.bySize.map((m) => (
                    <li key={m.key} className="flex justify-between"><span>{m.key}</span><b>{m.count}</b></li>
                  ))}
                </ul>
              </div>
            </div>
          </>
        )}
      </section>

      <section className="mb-10">
        <h2 className="mb-4 text-xl font-bold">{t.admin.stats}</h2>
        {stats.error && <ErrorBox error={stats.error} />}
        {stats.data && (
          <>
            <div className="mb-4 grid grid-cols-2 gap-3 md:grid-cols-5">
              <Stat label={t.admin.users} value={stats.data.totalUsers} />
              <Stat label={t.admin.teams} value={stats.data.totalTeams} />
              <Stat label={t.admin.surveys} value={stats.data.totalSurveys} />
              <Stat label={t.admin.responses} value={stats.data.totalResponses} />
              <Stat label={t.admin.overDeclared} value={stats.data.surveysOverDeclared} />
            </div>
            <div className="panel h-72">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={stats.data.days.map((d) => ({ ...d, day: d.day.slice(5) }))} margin={{ left: -20 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#a1a1aa55" />
                  <XAxis dataKey="day" tick={{ fontSize: 11 }} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Legend />
                  <Bar dataKey="registrations" name={t.admin.registrations} fill="#493082" />
                  <Bar dataKey="surveys" name={t.admin.surveys} fill="#FBAE17" />
                  <Bar dataKey="responses" name={t.admin.responses} fill="#00B16B" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </>
        )}
      </section>

      <section>
        <h2 className="mb-4 text-xl font-bold">{t.admin.users}</h2>
        <input className="input mb-3 max-w-md" placeholder={t.admin.search} value={q} onChange={(e) => setQ(e.target.value)} />
        {users.data && (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="text-left text-zinc-500">
                <tr><th className="py-2 pr-4">Email</th><th className="pr-4">Имя</th><th className="pr-4">Регистрация</th><th className="pr-4">Вход</th><th className="pr-4">{t.admin.teams}</th><th>{t.admin.surveys}</th></tr>
              </thead>
              <tbody>
                {users.data.map((u) => (
                  <tr key={u.id} className="border-t border-zinc-100 dark:border-zinc-800">
                    <td className="py-2 pr-4">{u.email}</td>
                    <td className="pr-4">{u.displayName ?? '—'}</td>
                    <td className="pr-4">{formatDate(u.createdAt)}</td>
                    <td className="pr-4">{formatDate(u.lastLoginAt)}</td>
                    <td className="pr-4">{u.teams}</td>
                    <td>{u.surveys}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </Page>
  )
}
