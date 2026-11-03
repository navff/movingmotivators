import { lazy, Suspense, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { keys } from '@/api/hooks'
import type { TeamDetails } from '@/api/types'
import { Page } from '@/components/Layout'
import { Empty, ErrorBox, Modal, PageTitle, Spinner, StatusBadge } from '@/components/ui'
import { formatDate, plural, t } from '@/i18n'

const HealthCheckHistoryView = lazy(() => import('../results/HealthCheckHistory'))
const MotivatorsCompare = lazy(() => import('../results/MotivatorsCompare'))

export default function TeamPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: team, isLoading, error, refetch } = useQuery({
    queryKey: keys.team(id),
    queryFn: () => api.get<TeamDetails>(`/teams/${id}`),
  })
  const [editing, setEditing] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const update = useMutation({
    mutationFn: (body: { name?: string; size?: number; archived?: boolean }) => api.patch<TeamDetails>(`/teams/${id}`, body),
    onSuccess: (d) => {
      qc.setQueryData(keys.team(id), d)
      qc.invalidateQueries({ queryKey: keys.teams })
      setEditing(false)
    },
  })
  const remove = useMutation({
    mutationFn: () => api.del(`/teams/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.teams })
      navigate('/app')
    },
  })

  if (isLoading) return <Spinner />
  if (error || !team) return <Page><ErrorBox error={error} onRetry={refetch} /></Page>

  const hcClosed = team.surveys.filter((s) => s.type === 'health_check' && s.status === 'closed')
  const mmSurveys = team.surveys.filter((s) => s.type === 'moving_motivators' && s.status !== 'draft' && s.responseCount > 0)

  return (
    <Page wide>
      <div className="mb-2 text-sm"><Link to="/app" className="text-zinc-500 hover:underline">← {t.teams.title}</Link></div>
      <PageTitle
        actions={
          <>
            <button className="btn-secondary" onClick={() => setEditing(true)}>{t.common.edit}</button>
            <Link to={`/app/teams/${id}/surveys/new`} className="btn-primary">{t.teams.newSurvey}</Link>
          </>
        }
      >
        {team.name}
        <span className="ml-3 align-middle text-base font-normal text-zinc-500">{team.size} {t.teams.people}</span>
      </PageTitle>

      <h2 className="mb-3 text-xl font-bold">{t.teams.surveysTitle}</h2>
      {team.surveys.length === 0 ? (
        <Empty action={<Link to={`/app/teams/${id}/surveys/new`} className="btn-primary">{t.teams.newSurvey}</Link>}>
          {t.teams.noSurveys}
        </Empty>
      ) : (
        <ul className="divide-y divide-zinc-200 overflow-hidden rounded-xl border border-zinc-200 bg-white dark:divide-zinc-800 dark:border-zinc-800 dark:bg-zinc-900">
          {team.surveys.map((s) => (
            <li key={s.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
              <div className="min-w-0">
                <Link to={s.checkoutRequired ? `/app/surveys/${s.id}/checkout` : `/app/surveys/${s.id}`} className="font-medium hover:underline">
                  {s.title}
                </Link>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-zinc-500">
                  <StatusBadge status={s.status} />
                  <span>{t.types[s.type]}</span>
                  <span>· {s.isAnonymous ? t.survey.anonymousBadge : t.survey.namedBadge}</span>
                  {s.status !== 'draft' && (
                    <span>· {s.responseCount}/{s.declaredParticipants} {plural(s.responseCount, ['ответ', 'ответа', 'ответов'])}</span>
                  )}
                  <span>· {formatDate(s.closedAt ?? s.createdAt)}</span>
                </div>
              </div>
              {s.checkoutRequired && (
                <Link to={`/app/surveys/${s.id}/checkout`} className="btn-primary min-h-9 py-1.5 text-sm">{t.teams.goToCheckout}</Link>
              )}
            </li>
          ))}
        </ul>
      )}

      <Suspense fallback={<Spinner />}>
        {hcClosed.length > 0 && (
          <section className="mt-10">
            <h2 className="mb-3 text-xl font-bold">{t.teams.historyTitle}</h2>
            <HealthCheckHistoryView teamId={id} />
          </section>
        )}
        {mmSurveys.length >= 2 && (
          <section className="mt-10">
            <h2 className="mb-1 text-xl font-bold">{t.teams.compareTitle}</h2>
            <p className="mb-3 text-sm text-zinc-500">{t.teams.compareHint}</p>
            <MotivatorsCompare surveys={mmSurveys} />
          </section>
        )}
      </Suspense>

      <EditTeamModal
        open={editing}
        team={team}
        busy={update.isPending}
        error={update.error}
        onClose={() => setEditing(false)}
        onSave={(name, size) => update.mutate({ name, size })}
        onArchive={() => update.mutate({ archived: !team.archivedAt })}
        onDelete={() => {
          setEditing(false)
          setDeleting(true)
        }}
      />
      <DeleteTeamModal open={deleting} team={team} busy={remove.isPending} onClose={() => setDeleting(false)} onConfirm={() => remove.mutate()} />
    </Page>
  )
}

function EditTeamModal({
  open, team, busy, error, onClose, onSave, onArchive, onDelete,
}: {
  open: boolean
  team: TeamDetails
  busy: boolean
  error: unknown
  onClose: () => void
  onSave: (name: string, size: number) => void
  onArchive: () => void
  onDelete: () => void
}) {
  const [name, setName] = useState(team.name)
  const [size, setSize] = useState(String(team.size))
  return (
    <Modal open={open} onClose={onClose} title={t.common.edit}>
      <form
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault()
          onSave(name.trim(), Number(size))
        }}
      >
        <div>
          <label className="label" htmlFor="team-name">{t.teams.name}</label>
          <input id="team-name" className="input" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="team-size">{t.teams.size}</label>
          <input id="team-size" className="input max-w-40" type="number" min={2} max={200} required value={size} onChange={(e) => setSize(e.target.value)} />
        </div>
        {error ? <p className="error-text">{errorMessage(error)}</p> : null}
        <div className="flex flex-wrap justify-between gap-2 pt-2">
          <div className="flex gap-2">
            <button type="button" className="btn-secondary" onClick={onArchive}>{team.archivedAt ? t.teams.unarchive : t.teams.archive}</button>
            <button type="button" className="btn-ghost text-red-600" onClick={onDelete}>{t.common.delete}</button>
          </div>
          <button className="btn-primary" disabled={busy}>{t.common.save}</button>
        </div>
      </form>
    </Modal>
  )
}

function DeleteTeamModal({ open, team, busy, onClose, onConfirm }: { open: boolean; team: TeamDetails; busy: boolean; onClose: () => void; onConfirm: () => void }) {
  const [typed, setTyped] = useState('')
  return (
    <Modal open={open} onClose={onClose} title={t.teams.deleteTeam}>
      <p className="mb-3 text-zinc-600 dark:text-zinc-300">{t.teams.deleteConfirm}</p>
      <p className="mb-2 font-mono text-sm">{team.name}</p>
      <input className="input mb-5" value={typed} onChange={(e) => setTyped(e.target.value)} aria-label={t.teams.name} />
      <div className="flex justify-end gap-2">
        <button className="btn-secondary" onClick={onClose}>{t.common.cancel}</button>
        <button className="btn-danger" disabled={typed.trim() !== team.name || busy} onClick={onConfirm}>{t.teams.deleteTeam}</button>
      </div>
    </Modal>
  )
}
