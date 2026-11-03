import { useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { keys, useCatalog } from '@/api/hooks'
import type { HealthCheckResult, MotivatorsResult, Survey, SurveyResult } from '@/api/types'
import { Page } from '@/components/Layout'
import { ConfirmModal, CopyButton, Empty, ErrorBox, Modal, Spinner, StatusBadge } from '@/components/ui'
import { fmt, formatDateTime, t } from '@/i18n'
import { participantUrl } from '../surveys/SharePage'
import { ChangesEditor, newChangeKey, type ChangeDraft } from '../surveys/ChangesEditor'
import { MotivatorsResults } from './MotivatorsResults'
import { HealthCheckResults } from './HealthCheckResults'

function toLocalInput(iso: string | null | undefined, fallbackDays = 7) {
  const d = iso && new Date(iso) > new Date() ? new Date(iso) : new Date(Date.now() + fallbackDays * 86400_000)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export default function SurveyPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: catalog } = useCatalog()
  const { data: survey, error, refetch } = useQuery({
    queryKey: keys.survey(id),
    queryFn: () => api.get<Survey>(`/surveys/${id}`),
    refetchInterval: (q) => (q.state.data?.status === 'open' ? 15_000 : false),
  })
  const { data: results } = useQuery({
    queryKey: keys.results(id),
    queryFn: () => api.get<SurveyResult>(`/surveys/${id}/results`),
    enabled: !!survey && survey.status !== 'draft',
    // пока опрос открыт — результаты в реальном времени
    refetchInterval: survey?.status === 'open' ? 15_000 : false,
  })

  const [confirmClose, setConfirmClose] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [reopenOpen, setReopenOpen] = useState(false)
  const [settingsOpen, setSettingsOpen] = useState(false)

  const onSurvey = (s: Survey) => {
    qc.setQueryData(keys.survey(id), s)
    qc.invalidateQueries({ queryKey: keys.results(id) })
    qc.invalidateQueries({ queryKey: keys.team(s.teamId) })
  }
  const close = useMutation({ mutationFn: () => api.post<Survey>(`/surveys/${id}/close`), onSuccess: (s) => { onSurvey(s); setConfirmClose(false) } })
  const remove = useMutation({
    mutationFn: () => api.del(`/surveys/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.teams })
      navigate(survey ? `/app/teams/${survey.teamId}` : '/app')
    },
  })

  if (error) return <Page><ErrorBox error={error} onRetry={refetch} /></Page>
  if (!survey || !catalog) return <Spinner />
  if (survey.checkoutRequired) return <Navigate to={`/app/surveys/${id}/checkout`} replace />

  const url = participantUrl(survey)

  return (
    <Page wide>
      <div className="mb-2 text-sm"><Link to={`/app/teams/${survey.teamId}`} className="text-zinc-500 hover:underline">← {survey.teamName}</Link></div>

      <header className="mb-6 space-y-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold sm:text-3xl">{survey.title}</h1>
            <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-zinc-600 dark:text-zinc-400">
              <StatusBadge status={survey.status} />
              <span>{t.types[survey.type]}</span>
              <span>· {survey.isAnonymous ? `🔒 ${t.survey.anonymousBadge}` : `👤 ${t.survey.namedBadge}`}</span>
              <span>
                ·{' '}
                {survey.status === 'closed'
                  ? fmt(t.survey.closedAt, { date: formatDateTime(survey.closedAt) })
                  : fmt(t.survey.closesAt, { date: formatDateTime(survey.closesAt) })}
              </span>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {survey.status === 'open' && (
              <>
                <button className="btn-secondary" onClick={() => setSettingsOpen(true)}>{t.survey.editSettings}</button>
                <button className="btn-primary" onClick={() => setConfirmClose(true)}>{t.survey.close}</button>
              </>
            )}
            {survey.canReopen && <button className="btn-secondary" onClick={() => setReopenOpen(true)}>{t.survey.reopen}</button>}
            <button className="btn-ghost text-red-600" onClick={() => setConfirmDelete(true)}>{t.common.delete}</button>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-3 rounded-xl bg-brand-50 p-4 dark:bg-zinc-900">
          <div className="font-slab text-lg font-bold">
            {fmt(t.survey.responded, { n: survey.responseCount, m: survey.declaredParticipants })}
          </div>
          {survey.responsesOverDeclared && <span className="text-sm text-amber-700 dark:text-amber-300">{t.survey.over}</span>}
          {survey.status === 'open' && (
            <div className="flex min-w-0 flex-1 flex-wrap items-center gap-2">
              <code className="min-w-0 truncate rounded bg-white px-2 py-1 text-sm dark:bg-zinc-800">{url}</code>
              <CopyButton text={url} className="btn-secondary min-h-9 py-1 text-sm" />
              <Link to={`/app/surveys/${id}/share`} className="btn-ghost min-h-9 py-1 text-sm">{t.survey.share}</Link>
            </div>
          )}
        </div>
        {survey.status === 'open' && survey.responseCount > 0 && <p className="text-sm text-zinc-500">{t.survey.liveNote}</p>}
      </header>

      {!results ? (
        <Spinner />
      ) : results.responseCount === 0 ? (
        <Empty>{t.survey.noResponses}</Empty>
      ) : results.type === 'moving_motivators' ? (
        <MotivatorsResults survey={survey} result={results as MotivatorsResult} catalog={catalog} />
      ) : (
        <HealthCheckResults survey={survey} result={results as HealthCheckResult} catalog={catalog} />
      )}

      <ConfirmModal open={confirmClose} title={t.survey.close} text={t.survey.closeConfirm} confirmLabel={t.survey.close}
        busy={close.isPending} onConfirm={() => close.mutate()} onClose={() => setConfirmClose(false)} />
      <ConfirmModal open={confirmDelete} title={t.survey.delete} text={t.survey.deleteConfirm} confirmLabel={t.common.delete} danger
        busy={remove.isPending} onConfirm={() => remove.mutate()} onClose={() => setConfirmDelete(false)} />
      {reopenOpen && <ReopenModal survey={survey} onClose={() => setReopenOpen(false)} onDone={onSurvey} />}
      {settingsOpen && <SettingsModal survey={survey} onClose={() => setSettingsOpen(false)} onDone={onSurvey} />}
    </Page>
  )
}

function ReopenModal({ survey, onClose, onDone }: { survey: Survey; onClose: () => void; onDone: (s: Survey) => void }) {
  const [closesAt, setClosesAt] = useState(toLocalInput(null))
  const reopen = useMutation({
    mutationFn: () => api.post<Survey>(`/surveys/${survey.id}/reopen`, { closesAt: new Date(closesAt).toISOString() }),
    onSuccess: (s) => { onDone(s); onClose() },
  })
  return (
    <Modal open onClose={onClose} title={t.survey.reopenTitle}>
      <label className="label" htmlFor="reopen-until">{t.survey.reopenUntil}</label>
      <input id="reopen-until" type="datetime-local" className="input mb-4" value={closesAt} onChange={(e) => setClosesAt(e.target.value)} />
      {reopen.isError && <p className="error-text mb-3">{errorMessage(reopen.error)}</p>}
      <div className="flex justify-end gap-2">
        <button className="btn-secondary" onClick={onClose}>{t.common.cancel}</button>
        <button className="btn-primary" disabled={reopen.isPending} onClick={() => reopen.mutate()}>{t.survey.reopen}</button>
      </div>
    </Modal>
  )
}

/** Правка опроса по правилам §5.3: после первого ответа — анонимность и состав изменений заблокированы. */
function SettingsModal({ survey, onClose, onDone }: { survey: Survey; onClose: () => void; onDone: (s: Survey) => void }) {
  const locked = survey.responseCount > 0
  const [title, setTitle] = useState(survey.title)
  const [closesAt, setClosesAt] = useState(toLocalInput(survey.closesAt))
  const [welcome, setWelcome] = useState(survey.welcomeMessage ?? '')
  const [anonymous, setAnonymous] = useState(survey.isAnonymous)
  const [changes, setChanges] = useState<ChangeDraft[]>(survey.settings.changes.map((c) => ({ key: newChangeKey(), id: c.id, text: c.text })))
  const save = useMutation({
    mutationFn: () =>
      api.patch<Survey>(`/surveys/${survey.id}`, {
        title,
        closesAt: new Date(closesAt).toISOString(),
        welcomeMessage: welcome,
        isAnonymous: locked ? undefined : anonymous,
        changes: survey.type === 'moving_motivators' && survey.settings.changes.length > 0
          ? changes.map((c) => ({ id: c.id, text: c.text }))
          : undefined,
      }),
    onSuccess: (s) => { onDone(s); onClose() },
  })
  return (
    <Modal open onClose={onClose} title={t.survey.editSettings}>
      <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); save.mutate() }}>
        <div>
          <label className="label" htmlFor="s-title">{t.wizard.surveyTitle}</label>
          <input id="s-title" className="input" required maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="s-closes">{t.wizard.closesAt}</label>
          <input id="s-closes" type="datetime-local" className="input" value={closesAt} onChange={(e) => setClosesAt(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="s-welcome">{t.wizard.welcome}</label>
          <textarea id="s-welcome" className="input" rows={3} maxLength={500} value={welcome} onChange={(e) => setWelcome(e.target.value)} />
        </div>
        <label className="flex items-center gap-3 text-sm">
          <input type="checkbox" className="h-5 w-5 accent-brand" checked={anonymous} disabled={locked} onChange={(e) => setAnonymous(e.target.checked)} />
          {t.wizard.anonymous}
        </label>
        {locked && <p className="hint">{t.wizard.anonymityLocked}</p>}
        {survey.settings.changes.length > 0 && (
          <div>
            <div className="label">{t.wizard.impact}</div>
            <ChangesEditor value={changes} onChange={setChanges} locked={locked} />
          </div>
        )}
        {save.isError && <p className="error-text">{errorMessage(save.error)}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onClose}>{t.common.cancel}</button>
          <button className="btn-primary" disabled={save.isPending}>{t.common.save}</button>
        </div>
      </form>
    </Modal>
  )
}
