import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, errorMessage } from '@/api/client'
import { keys, useCatalog, usePricing } from '@/api/hooks'
import type { CreateSurveyResponse, SurveyType, TeamDetails } from '@/api/types'
import { Page } from '@/components/Layout'
import { ErrorBox, PageTitle, Spinner } from '@/components/ui'
import { fmt, monthYear, t } from '@/i18n'
import { ChangesEditor, newChangeKey, type ChangeDraft } from './ChangesEditor'

function toLocalInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export default function NewSurveyPage() {
  const { id: teamId = '' } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: team, error: teamError } = useQuery({ queryKey: keys.team(teamId), queryFn: () => api.get<TeamDetails>(`/teams/${teamId}`) })
  const { data: pricing } = usePricing()
  const { data: catalog } = useCatalog()

  const [type, setType] = useState<SurveyType>('moving_motivators')
  const [title, setTitle] = useState<string | null>(null)
  const [participants, setParticipants] = useState<string | null>(null)
  const [anonymous, setAnonymous] = useState(true)
  const [impactOn, setImpactOn] = useState(false)
  const [changes, setChanges] = useState<ChangeDraft[]>([{ key: newChangeKey(), text: '' }])
  const [disabledCats, setDisabledCats] = useState<string[]>([])
  const [closesAt, setClosesAt] = useState(() => toLocalInput(new Date(Date.now() + 7 * 86400_000)))
  const [autoClose, setAutoClose] = useState(false)
  const [welcome, setWelcome] = useState('')
  const [showHowTo, setShowHowTo] = useState(false)

  const defaultTitle = `${t.types[type]} · ${monthYear(new Date())}`
  const n = Number(participants ?? team?.size ?? 0)
  const paid = pricing ? n > pricing.freeMaxParticipants : false

  const create = useMutation({
    mutationFn: () =>
      api.post<CreateSurveyResponse>(`/teams/${teamId}/surveys`, {
        type,
        title: (title ?? defaultTitle).trim() || defaultTitle,
        declaredParticipants: n,
        isAnonymous: anonymous,
        changes: type === 'moving_motivators' ? (impactOn ? changes.map((c) => c.text.trim()) : []) : undefined,
        categories: type === 'health_check'
          ? catalog?.healthCheckCategories.map((c) => c.key).filter((k) => !disabledCats.includes(k))
          : undefined,
        closesAt: new Date(closesAt).toISOString(),
        welcomeMessage: welcome.trim() || undefined,
        autoCloseOnFull: autoClose,
      }),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: keys.team(teamId) })
      qc.invalidateQueries({ queryKey: keys.teams })
      navigate(r.checkoutRequired ? `/app/surveys/${r.survey.id}/checkout` : `/app/surveys/${r.survey.id}/share`)
    },
  })

  if (teamError) return <Page><ErrorBox error={teamError} /></Page>
  if (!team || !pricing || !catalog) return <Spinner />

  const fieldErrors = create.error instanceof ApiError ? create.error.errors ?? {} : {}
  const enabledCount = catalog.healthCheckCategories.length - disabledCats.length

  return (
    <Page>
      <div className="mb-2 text-sm"><Link to={`/app/teams/${teamId}`} className="text-zinc-500 hover:underline">← {team.name}</Link></div>
      <PageTitle>{t.wizard.title}</PageTitle>

      <form
        className="space-y-6"
        onSubmit={(e) => {
          e.preventDefault()
          create.mutate()
        }}
      >
        {/* 1. Тип */}
        <fieldset className="panel">
          <legend className="sr-only">{t.wizard.type}</legend>
          <div className="mb-1 label">{t.wizard.type}</div>
          <div className="grid gap-3 sm:grid-cols-2">
            {(['moving_motivators', 'health_check'] as const).map((tp) => (
              <label key={tp} className={`cursor-pointer rounded-lg border-2 p-4 ${type === tp ? 'border-brand bg-brand-50 dark:bg-zinc-800' : 'border-zinc-200 dark:border-zinc-700'}`}>
                <input type="radio" name="type" className="sr-only" checked={type === tp} onChange={() => setType(tp)} />
                <div className="font-slab font-bold">{t.types[tp]}</div>
                <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
                  {tp === 'moving_motivators' ? t.wizard.mmDescription : t.wizard.hcDescription}
                </div>
              </label>
            ))}
          </div>
          <button type="button" className="mt-3 text-sm text-brand underline dark:text-violet-300" onClick={() => setShowHowTo(!showHowTo)} aria-expanded={showHowTo}>
            {t.wizard.howTo} {showHowTo ? '▴' : '▾'}
          </button>
          {showHowTo && (
            <ol className="mt-2 list-decimal space-y-1 pl-5 text-sm text-zinc-600 dark:text-zinc-300">
              {(type === 'moving_motivators' ? t.wizard.mmHowTo : t.wizard.hcHowTo).map((s) => <li key={s}>{s}</li>)}
            </ol>
          )}
        </fieldset>

        <div className="panel space-y-5">
          {/* 2. Название */}
          <div>
            <label className="label" htmlFor="title">{t.wizard.surveyTitle}</label>
            <input id="title" className="input" maxLength={200} value={title ?? defaultTitle} onChange={(e) => setTitle(e.target.value)} />
          </div>

          {/* 3. Участники и цена */}
          <div>
            <label className="label" htmlFor="participants">{t.wizard.participants}</label>
            <div className="flex flex-wrap items-center gap-3">
              <input id="participants" className="input max-w-32" type="number" inputMode="numeric" min={2} max={200} required
                value={participants ?? String(team.size)} onChange={(e) => setParticipants(e.target.value)} />
              <span className={`rounded-full px-3 py-1 text-sm font-medium ${paid ? 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200' : 'bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300'}`}>
                {paid
                  ? fmt(t.wizard.paid, { n, price: pricing.surveyPriceRub })
                  : fmt(t.wizard.free, { n: pricing.freeMaxParticipants })}
              </span>
            </div>
            <p className="hint">{t.wizard.participantsHint}</p>
            {fieldErrors.declaredParticipants && <p className="error-text">{fieldErrors.declaredParticipants[0]}</p>}
          </div>
        </div>

        {/* 4. Анонимность */}
        <fieldset className="panel">
          <legend className="mb-2 label">{t.wizard.anonymity}</legend>
          <div className="grid gap-3 sm:grid-cols-2">
            {[true, false].map((anon) => (
              <label key={String(anon)} className={`cursor-pointer rounded-lg border-2 p-4 ${anonymous === anon ? 'border-brand bg-brand-50 dark:bg-zinc-800' : 'border-zinc-200 dark:border-zinc-700'}`}>
                <input type="radio" name="anon" className="sr-only" checked={anonymous === anon} onChange={() => setAnonymous(anon)} />
                <div className="font-bold">{anon ? `🔒 ${t.wizard.anonymous}` : `👤 ${t.wizard.named}`}</div>
                <div className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">{anon ? t.wizard.anonymousText : t.wizard.namedText}</div>
              </label>
            ))}
          </div>
          <p className="hint">{t.wizard.anonymityLocked}</p>
        </fieldset>

        {/* 5. Специфично для типа */}
        {type === 'moving_motivators' ? (
          <div className="panel">
            <label className="flex items-center gap-3 font-medium">
              <input type="checkbox" className="h-5 w-5 accent-brand" checked={impactOn} onChange={(e) => setImpactOn(e.target.checked)} />
              {t.wizard.impact}
            </label>
            <p className="hint ml-8">{t.wizard.impactHint}</p>
            {impactOn && (
              <div className="mt-4">
                <ChangesEditor value={changes} onChange={setChanges} />
                {fieldErrors.changes && <p className="error-text mt-2">{fieldErrors.changes[0]}</p>}
              </div>
            )}
          </div>
        ) : (
          <fieldset className="panel">
            <legend className="mb-1 label">{t.wizard.categories}</legend>
            <p className="hint mb-3">{t.wizard.categoriesHint}</p>
            <div className="grid gap-2 sm:grid-cols-2">
              {catalog.healthCheckCategories.map((c) => {
                const on = !disabledCats.includes(c.key)
                return (
                  <label key={c.key} className="flex items-start gap-3 rounded-lg p-2 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                    <input
                      type="checkbox"
                      className="mt-0.5 h-5 w-5 accent-brand"
                      checked={on}
                      disabled={on && enabledCount <= 3}
                      onChange={() => setDisabledCats(on ? [...disabledCats, c.key] : disabledCats.filter((k) => k !== c.key))}
                    />
                    <span>
                      <span className="font-medium">{c.name}</span>
                      <span className="block text-xs text-zinc-500">{c.awesome}</span>
                    </span>
                  </label>
                )
              })}
            </div>
          </fieldset>
        )}

        {/* 6–7. Срок и приветствие */}
        <div className="panel space-y-5">
          <div>
            <label className="label" htmlFor="closesAt">{t.wizard.closesAt}</label>
            <input id="closesAt" className="input max-w-64" type="datetime-local" required value={closesAt}
              min={toLocalInput(new Date())} onChange={(e) => setClosesAt(e.target.value)} />
            {fieldErrors.closesAt && <p className="error-text">{fieldErrors.closesAt[0]}</p>}
          </div>
          <label className="flex items-center gap-3 text-sm">
            <input type="checkbox" className="h-5 w-5 accent-brand" checked={autoClose} onChange={(e) => setAutoClose(e.target.checked)} />
            {t.wizard.autoClose}
          </label>
          <div>
            <label className="label" htmlFor="welcome">{t.wizard.welcome} <span className="font-normal text-zinc-400">({t.common.optional})</span></label>
            <textarea id="welcome" className="input" rows={3} maxLength={500} placeholder={t.wizard.welcomePlaceholder}
              value={welcome} onChange={(e) => setWelcome(e.target.value)} />
            <p className="hint text-right">{welcome.length}/500</p>
          </div>
        </div>

        {create.isError && <p className="error-text" role="alert">{errorMessage(create.error)}</p>}

        <button className="btn-primary w-full sm:w-auto" disabled={create.isPending || (type === 'moving_motivators' && impactOn && changes.some((c) => !c.text.trim()))}>
          {paid ? fmt(t.wizard.toCheckout, { price: pricing.surveyPriceRub }) : t.wizard.launch}
        </button>
      </form>
    </Page>
  )
}
