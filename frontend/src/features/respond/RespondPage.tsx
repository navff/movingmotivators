import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { api, ApiError, errorMessage } from '@/api/client'
import type { HealthCheckAnswer, ImpactMarks, MotivatorCardInfo, MyResponse, PublicSurvey } from '@/api/types'
import { MotivatorCard } from '@/components/MotivatorCard'
import { MotivatorsMap } from '@/components/MotivatorsMap'
import { ErrorBox, Spinner } from '@/components/ui'
import { shuffle, storage, uuid } from '@/lib/storage'
import { trendArrow } from '@/lib/colors'
import { fmt, t } from '@/i18n'
import { RankingStep } from './RankingStep'
import { ImpactStep } from './ImpactStep'
import { RatingStep } from './RatingStep'
import { HealthCheckCategoryForm, isComplete, type HcDraft } from './HealthCheckStep'
import { useIsDesktop } from './useMediaQuery'

interface Draft {
  stage: 'welcome' | 'steps' | 'done'
  step: number
  startedAt: number
  name: string
  ranking: string[]
  /** Карточка → 1..5: насколько мотиватор развит в команде. Привязано к ключу, а не к позиции. */
  ratings: Record<string, number>
  /** Карточка → комментарий к оценке (необязательно). */
  ratingComments: Record<string, string>
  /** changeId → (карточка → −1|0|1). Привязано к ключу карточки, а не к позиции. */
  impact: ImpactMarks
  answers: Record<string, HcDraft>
  comment: string
}

const draftKey = (code: string) => `mm:draft:${code}`
const respondentKeyFor = (code: string) => {
  const k = `mm:rk:${code}`
  let v = storage.get<string>(k)
  if (!v) {
    v = uuid()
    storage.set(k, v)
  }
  return v
}

function emptyDraft(s: PublicSurvey): Draft {
  return {
    stage: 'welcome',
    step: 0,
    startedAt: Date.now(),
    name: '',
    ranking: shuffle((s.cards ?? []).map((c) => c.key)),
    ratings: {},
    ratingComments: {},
    impact: Object.fromEntries((s.changes ?? []).map((c) => [c.id, Object.fromEntries((s.cards ?? []).map((k) => [k.key, 0]))])),
    answers: {},
    comment: '',
  }
}

export default function RespondPage() {
  const { code = '' } = useParams()
  const { data: survey, error, isLoading } = useQuery({
    queryKey: ['public', code],
    queryFn: () => api.get<PublicSurvey>(`/public/surveys/${code}`),
    retry: false,
  })

  return (
    <div className="min-h-dvh bg-zinc-50 dark:bg-zinc-950">
      <header className="border-b border-zinc-200 bg-white px-4 py-3 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-[1400px] items-center gap-2 font-slab text-sm font-bold text-brand dark:text-violet-300">
          <img src="/favicon.svg" alt="" className="h-6 w-6" />
          {t.common.productName}
        </div>
      </header>
      <main className="px-4 py-6">
        {isLoading && <Spinner />}
        {(error instanceof ApiError && error.status === 404) || survey?.status === 'closed' ? (
          <Closed />
        ) : error ? (
          <div className="mx-auto max-w-xl"><ErrorBox error={error} /></div>
        ) : survey ? (
          <Flow survey={survey} />
        ) : null}
      </main>
      <footer className="px-4 pb-6 text-center text-xs text-zinc-400">
        {survey?.type === 'health_check' ? t.footer.hcCredit : t.footer.mmCopyright} ·{' '}
        <Link to="/privacy" className="underline">{t.footer.privacy}</Link>
      </footer>
    </div>
  )
}

function Closed() {
  return (
    <div className="panel mx-auto max-w-xl text-center">
      <h1 className="mb-2 text-2xl font-bold">{t.respond.closedTitle}</h1>
      <p className="text-zinc-600 dark:text-zinc-300">{t.respond.closedText}</p>
    </div>
  )
}

function Flow({ survey }: { survey: PublicSurvey }) {
  const code = survey.code
  const isMm = survey.type === 'moving_motivators'
  const desktop = useIsDesktop()
  const cards = useMemo(() => new Map<string, MotivatorCardInfo>((survey.cards ?? []).map((c) => [c.key, c])), [survey.cards])
  const categories = survey.categories ?? []
  const changes = survey.changes ?? []

  const [draft, setDraftState] = useState<Draft>(() => {
    const saved = storage.get<Draft>(draftKey(code))
    const fresh = emptyDraft(survey)
    if (!saved) return fresh
    // черновик от другой конфигурации опроса — подчищаем
    const validRanking = saved.ranking?.length === fresh.ranking.length && saved.ranking.every((k) => cards.has(k))
    return { ...fresh, ...saved, ranking: validRanking ? saved.ranking : fresh.ranking, impact: { ...fresh.impact, ...saved.impact } }
  })
  const setDraft = (patch: Partial<Draft>) =>
    setDraftState((d) => {
      const next = { ...d, ...patch }
      storage.set(draftKey(code), next)
      return next
    })

  const [already, setAlready] = useState(false)
  const [consent, setConsent] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [closed, setClosed] = useState(false)
  const honeypot = useRef<HTMLInputElement>(null)
  const top = useRef<HTMLDivElement>(null)

  // уже отвечал с этого устройства? — подтягиваем ответ для правки
  useEffect(() => {
    api.post<MyResponse | undefined>(`/public/surveys/${code}/responses/mine`, { respondentKey: respondentKeyFor(code) })
      .then((mine) => {
        if (!mine) return
        setAlready(true)
        setDraftState((d) => {
          if (d.stage !== 'welcome') return d
          return {
            ...d,
            name: mine.participantName ?? d.name,
            ranking: mine.payload.ranking ?? d.ranking,
            ratings: mine.payload.ratings ?? d.ratings,
            ratingComments: mine.payload.ratingComments ?? d.ratingComments,
            impact: { ...d.impact, ...(mine.payload.impact ?? {}) },
            answers: (mine.payload.answers as Record<string, HcDraft>) ?? d.answers,
            comment: mine.comment ?? d.comment,
          }
        })
      })
      .catch(() => {})
  }, [code])

  // шаги
  type Step =
    | { kind: 'rank' } | { kind: 'rate'; i: number } | { kind: 'impact'; i: number }
    | { kind: 'hc-all' } | { kind: 'hc'; i: number } | { kind: 'final' }
  // опросы методики v1 приходят без описаний уровней — шага оценки у них нет
  const hasRatings = isMm && (survey.cards ?? []).every((c) => c.levels?.length)
  // от самого важного для участника, как и на шаге влияния
  const rateOrder = [...draft.ranking].reverse()
  const steps: Step[] = isMm
    ? [
      { kind: 'rank' },
      ...(hasRatings ? rateOrder.map((_, i) => ({ kind: 'rate' as const, i })) : []),
      ...changes.map((_, i) => ({ kind: 'impact' as const, i })),
      { kind: 'final' },
    ]
    : [...(desktop ? [{ kind: 'hc-all' as const }] : categories.map((_, i) => ({ kind: 'hc' as const, i }))), { kind: 'final' }]
  const stepIndex = Math.min(draft.step, steps.length - 1)
  const step = steps[stepIndex]

  const go = (delta: number) => {
    setDraft({ step: Math.max(0, Math.min(steps.length - 1, stepIndex + delta)) })
    top.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  const canNext =
    step.kind === 'rate' ? draft.ratings[rateOrder[step.i]] != null
    : step.kind === 'hc' ? isComplete(draft.answers[categories[step.i].key])
    : step.kind === 'hc-all' ? categories.every((c) => isComplete(draft.answers[c.key]))
    : true
  const needsName = !survey.isAnonymous
  const canSubmit = (!needsName || (draft.name.trim().length > 0 && consent)) &&
    (isMm
      ? !hasRatings || rateOrder.every((k) => draft.ratings[k] != null)
      : categories.every((c) => isComplete(draft.answers[c.key])))

  const submit = async () => {
    setSubmitting(true)
    setSubmitError(null)
    try {
      const payload = isMm
        ? {
          ranking: draft.ranking,
          ratings: hasRatings ? draft.ratings : undefined,
          ratingComments: hasRatings ? draft.ratingComments : undefined,
          impact: changes.length ? Object.fromEntries(changes.map((c) => [c.id, draft.impact[c.id]])) : null,
        }
        : {
          answers: Object.fromEntries(categories.map((c) => {
            const a = draft.answers[c.key]
            return [c.key, { score: a.score, trend: a.trend, comment: a.comment?.trim() || null }]
          })),
        }
      await api.post(`/public/surveys/${code}/responses`, {
        respondentKey: respondentKeyFor(code),
        participantName: needsName ? draft.name.trim() : undefined,
        payload,
        comment: draft.comment.trim() || undefined,
        consent: needsName ? consent : undefined,
        honeypot: honeypot.current?.value ?? '',
        startedAt: draft.startedAt,
      })
      setDraft({ stage: 'done' })
      setAlready(true)
      window.scrollTo({ top: 0 })
    } catch (e) {
      if (e instanceof ApiError && e.errorCode === 'survey_closed') setClosed(true)
      else if (e instanceof ApiError && e.errorCode === 'too_fast') setSubmitError(t.respond.tooFast)
      else setSubmitError(errorMessage(e))
    } finally {
      setSubmitting(false)
    }
  }

  if (closed) return <Closed />

  // ---------- приветствие ----------
  if (draft.stage === 'welcome') {
    return (
      <div className="mx-auto max-w-xl space-y-5">
        <div className="panel space-y-4">
          <div className="text-sm text-zinc-500">{t.respond.team}: <b className="text-zinc-800 dark:text-zinc-200">{survey.teamName}</b></div>
          <h1 className="text-2xl font-bold">{t.types[survey.type!]}</h1>
          {survey.welcomeMessage && (
            <blockquote className="whitespace-pre-wrap rounded-lg border-l-4 border-brand bg-brand-50 p-3 dark:bg-zinc-800">{survey.welcomeMessage}</blockquote>
          )}
          <div className={`rounded-lg p-4 ${survey.isAnonymous ? 'bg-green-50 dark:bg-green-950/40' : 'bg-amber-50 dark:bg-amber-950/40'}`}>
            <div className="mb-1 text-lg font-bold">{survey.isAnonymous ? t.respond.anonBadge : t.respond.namedBadge}</div>
            <p className="text-sm">{survey.isAnonymous ? t.respond.anonText : t.respond.namedText}</p>
          </div>
          <p className="text-sm text-zinc-500">⏱ {fmt(t.respond.takes, { minutes: hasRatings ? 7 : 5 })}</p>
          {already && <p className="rounded-lg bg-blue-50 p-3 text-sm text-blue-900 dark:bg-blue-950 dark:text-blue-200">{t.respond.alreadyDone}</p>}
          {needsName && (
            <div>
              <label className="label" htmlFor="pname">{t.respond.yourName}</label>
              <input id="pname" className="input" maxLength={60} autoComplete="name" placeholder={t.respond.yourNamePlaceholder}
                value={draft.name} onChange={(e) => setDraft({ name: e.target.value })} />
            </div>
          )}
          <button
            className="btn-primary w-full"
            disabled={needsName && !draft.name.trim()}
            onClick={() => setDraft({ stage: 'steps', step: 0, startedAt: Date.now() })}
          >
            {t.respond.start}
          </button>
        </div>
      </div>
    )
  }

  // ---------- спасибо ----------
  if (draft.stage === 'done') {
    return (
      <div className="mx-auto max-w-3xl space-y-5">
        <div className="panel">
          <h1 className="mb-2 text-2xl font-bold">{t.respond.thanksTitle}</h1>
          <p className="text-zinc-600 dark:text-zinc-300">{isMm ? t.respond.thanksText : t.respond.thanksHcText}</p>
        </div>
        {isMm && hasRatings ? (
          <MotivatorsMap title={t.respond.mapTitle} hint={t.respond.mapHint} cards={cards} ranking={draft.ranking} ratings={draft.ratings} />
        ) : isMm ? (
          <>
            <h2 className="font-bold">{t.respond.yourRanking}</h2>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-5">
              {[...draft.ranking].reverse().map((k, i) => (
                <MotivatorCard key={k} card={cards.get(k)!} showEnglish={false}
                  badge={<span className="rounded-full bg-zinc-900 px-2 py-0.5 text-xs font-bold text-white">{i + 1}</span>} />
              ))}
            </div>
          </>
        ) : (
          <div className="panel divide-y divide-zinc-100 dark:divide-zinc-800">
            {categories.map((c) => {
              const a = draft.answers[c.key] as HealthCheckAnswer
              return (
                <div key={c.key} className="flex items-center justify-between py-2">
                  <span>{c.name}</span>
                  <span className="text-lg">{a?.score === 3 ? '🟢' : a?.score === 2 ? '🟡' : '🔴'} {trendArrow(a?.trend)}</span>
                </div>
              )
            })}
          </div>
        )}
        <button className="btn-secondary" onClick={() => setDraft({ stage: 'steps', step: 0, startedAt: Date.now() })}>{t.respond.changeAnswer}</button>
      </div>
    )
  }

  // ---------- шаги ----------
  const progress = ((stepIndex + 1) / steps.length) * 100
  const wide = step.kind === 'rank' && desktop

  return (
    <div ref={top} className={`mx-auto scroll-mt-4 ${wide ? 'max-w-[1400px]' : 'max-w-2xl'}`}>
      <div className="mb-5">
        <div className="mb-1 flex justify-between text-xs text-zinc-500">
          <span>{fmt(t.respond.step, { i: stepIndex + 1, n: steps.length })}</span>
        </div>
        <div className="h-2 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-800" role="progressbar" aria-valuenow={Math.round(progress)} aria-valuemin={0} aria-valuemax={100}>
          <div className="h-full rounded-full bg-brand transition-all" style={{ width: `${progress}%` }} />
        </div>
      </div>

      {step.kind === 'rank' && (
        <>
          <h1 className="mb-2 text-2xl font-bold">{t.respond.rankTitle}</h1>
          <RankingStep cards={cards} ranking={draft.ranking} onChange={(ranking) => setDraft({ ranking })} />
        </>
      )}

      {step.kind === 'rate' && (
        <RatingStep
          card={cards.get(rateOrder[step.i])!}
          index={step.i}
          total={rateOrder.length}
          value={draft.ratings[rateOrder[step.i]]}
          onChange={(v) => setDraft({ ratings: { ...draft.ratings, [rateOrder[step.i]]: v } })}
          comment={draft.ratingComments[rateOrder[step.i]] ?? ''}
          onCommentChange={(v) => setDraft({ ratingComments: { ...draft.ratingComments, [rateOrder[step.i]]: v } })}
        />
      )}

      {step.kind === 'impact' && (
        <ImpactStep
          change={changes[step.i]}
          index={step.i}
          total={changes.length}
          cards={cards}
          ranking={draft.ranking}
          marks={draft.impact[changes[step.i].id] ?? {}}
          onChange={(m) => setDraft({ impact: { ...draft.impact, [changes[step.i].id]: m } })}
        />
      )}

      {step.kind === 'hc' && (
        <HealthCheckCategoryForm
          category={categories[step.i]}
          value={draft.answers[categories[step.i].key] ?? {}}
          onChange={(v) => setDraft({ answers: { ...draft.answers, [categories[step.i].key]: v } })}
        />
      )}

      {step.kind === 'hc-all' && (
        <div className="space-y-4">
          {categories.map((c) => (
            <HealthCheckCategoryForm key={c.key} category={c} value={draft.answers[c.key] ?? {}}
              onChange={(v) => setDraft({ answers: { ...draft.answers, [c.key]: v } })} />
          ))}
        </div>
      )}

      {step.kind === 'final' && (
        <div className="panel space-y-5">
          <div>
            <label className="label text-base" htmlFor="comment">{t.respond.commentTitle}</label>
            <textarea id="comment" className="input" rows={4} maxLength={1000} placeholder={t.respond.commentPlaceholder}
              value={draft.comment} onChange={(e) => setDraft({ comment: e.target.value })} />
            <p className="hint text-right">{draft.comment.length}/1000</p>
          </div>
          {needsName && (
            <>
              <div>
                <label className="label" htmlFor="pname2">{t.respond.yourName}</label>
                <input id="pname2" className="input" maxLength={60} value={draft.name} onChange={(e) => setDraft({ name: e.target.value })} />
              </div>
              <label className="flex items-start gap-3 text-sm">
                <input type="checkbox" className="mt-1 h-5 w-5 shrink-0 accent-brand" checked={consent} onChange={(e) => setConsent(e.target.checked)} />
                <span>
                  {fmt(t.respond.consentPrefix, { organizer: survey.organizerName ?? '' })}
                  <Link to="/consent#participant" target="_blank" className="text-brand underline dark:text-violet-300">{t.respond.consentMore}</Link>
                </span>
              </label>
            </>
          )}
          {/* honeypot: невидимое поле для ботов */}
          <input ref={honeypot} type="text" name="website" tabIndex={-1} autoComplete="off" aria-hidden className="absolute -left-[9999px] h-px w-px opacity-0" />
          {submitError && <p className="error-text" role="alert">{submitError}</p>}
        </div>
      )}

      <div className="sticky bottom-0 -mx-4 mt-6 flex justify-between gap-3 border-t border-zinc-200 bg-zinc-50/95 px-4 py-3 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/95 sm:static sm:mx-0 sm:border-0 sm:bg-transparent sm:px-0">
        <button className="btn-secondary" onClick={() => (stepIndex === 0 ? setDraft({ stage: 'welcome' }) : go(-1))}>{t.common.back}</button>
        {step.kind === 'final' ? (
          <button className="btn-primary flex-1 sm:flex-none" disabled={!canSubmit || submitting} onClick={submit}>{t.respond.submit}</button>
        ) : (
          <button className="btn-primary flex-1 sm:flex-none" disabled={!canNext} onClick={() => go(1)} title={!canNext ? t.respond.fillAll : undefined}>
            {t.common.next}
          </button>
        )}
      </div>
    </div>
  )
}
