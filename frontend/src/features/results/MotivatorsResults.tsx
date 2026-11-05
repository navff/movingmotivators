import { useState } from 'react'
import type { Catalog, MotivatorsResult, Survey } from '@/api/types'
import { MotivatorCard } from '@/components/MotivatorCard'
import { MotivatorsMap } from '@/components/MotivatorsMap'
import { Tabs } from '@/components/ui'
import { impactColor } from '@/lib/colors'
import { t } from '@/i18n'
import { cardMap, Chip, Comments, Prompts, signed } from './common'

type Tab = 'team' | 'impact' | 'participants' | 'comments' | 'discussion'

export function MotivatorsResults({ survey, result, catalog }: { survey: Survey; result: MotivatorsResult; catalog: Catalog }) {
  const hasImpact = result.changes.length > 0
  const tabs: { id: Tab; label: string }[] = [
    { id: 'team', label: t.results.tabTeam },
    ...(hasImpact ? [{ id: 'impact' as const, label: t.results.tabImpact }] : []),
    ...(!survey.isAnonymous ? [{ id: 'participants' as const, label: t.results.tabParticipants }] : []),
    { id: 'comments', label: `${t.results.tabComments} (${result.comments.length})` },
    { id: 'discussion', label: t.results.tabDiscussion },
  ]
  const [tab, setTab] = useState<Tab>('team')
  const cards = cardMap(catalog)

  return (
    <>
      <Tabs tabs={tabs} value={tab} onChange={setTab} />
      {tab === 'team' && <TeamTab result={result} cards={cards} />}
      {tab === 'impact' && <ImpactTab result={result} cards={cards} />}
      {tab === 'participants' && <ParticipantsTab result={result} cards={cards} />}
      {tab === 'comments' && <Comments items={result.comments} anonymous={survey.isAnonymous} />}
      {tab === 'discussion' && <Prompts prompts={result.prompts} />}
    </>
  )
}

type Cards = ReturnType<typeof cardMap>

function TeamTab({ result, cards }: { result: MotivatorsResult; cards: Cards }) {
  const consensusLabel = { unanimous: t.results.unanimous, mixed: t.results.mixed, divided: t.results.divided }
  return (
    <div className="space-y-10">
      <section>
        <h2 className="mb-4 text-xl font-bold">{t.results.top3}</h2>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:max-w-3xl">
          {result.top3.map((k, i) => (
            <MotivatorCard key={k} card={cards.get(k)!} badge={<span className="rounded-full bg-zinc-900 px-2 py-0.5 text-xs font-bold text-white">{i + 1}</span>} />
          ))}
        </div>
        <div className="mt-5 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
          <span className="text-zinc-500">{t.results.bottom3}:</span>
          {result.bottom3.map((k) => <Chip key={k} card={cards.get(k)} />)}
        </div>
      </section>

      <section>
        <h2 className="mb-1 text-xl font-bold">{t.results.ranking}</h2>
        <p className="mb-4 text-sm text-zinc-500">{t.results.rankingHint}</p>
        <div className="space-y-2">
          {result.cards.map((c) => {
            const card = cards.get(c.key)!
            return (
              <div key={c.key} className="grid grid-cols-[8.5rem_1fr_auto] items-center gap-3 sm:grid-cols-[11rem_1fr_13rem]">
                <Chip card={card} />
                <div className="h-6 rounded bg-zinc-100 dark:bg-zinc-800">
                  <div className="flex h-6 items-center justify-end rounded pr-2 text-xs font-bold" style={{ width: `${c.avgRank * 10}%`, background: card.accentColor }}>
                    <span className="rounded bg-white/85 px-1 text-zinc-900">{c.avgRank.toFixed(1)}</span>
                  </div>
                </div>
                <span className={`text-xs ${c.consensus === 'divided' ? 'font-medium text-amber-700 dark:text-amber-400' : 'text-zinc-500'}`}>
                  <span className="hidden sm:inline">σ {c.stdRank.toFixed(1)} · </span>
                  {consensusLabel[c.consensus]}
                </span>
              </div>
            )
          })}
        </div>
      </section>

      <RatingsSection result={result} cards={cards} />

      <section>
        <h2 className="mb-1 text-xl font-bold">{t.results.heatmap}</h2>
        <p className="mb-4 text-sm text-zinc-500">{t.results.heatmapHint}</p>
        <Heatmap result={result} cards={cards} />
      </section>
    </div>
  )
}

/** Важность vs развитость: сверху самое важное, подсвечены важные и слабо развитые (порог считает backend). */
function RatingsSection({ result, cards }: { result: MotivatorsResult; cards: Cards }) {
  if (!result.cards.some((c) => c.avgRating != null)) return null
  const weak = new Set(result.prompts.filter((p) => p.kind === 'low_rated_top_card').map((p) => p.cardKey))
  return (
    <section>
      <h2 className="mb-1 text-xl font-bold">{t.results.ratingTitle}</h2>
      <p className="mb-4 text-sm text-zinc-500">{t.results.ratingHint}</p>
      <div className="mb-6 max-w-3xl">
        <MotivatorsMap
          title={t.results.ratingMapTitle}
          hint={t.results.ratingMapHint}
          cards={cards}
          ranking={result.cards.map((c) => c.key).reverse()}
          ratings={Object.fromEntries(result.cards.map((c) => [c.key, c.avgRating ?? 0]))}
        />
      </div>
      <div className="space-y-2">
        {result.cards.map((c) => {
          const card = cards.get(c.key)!
          const avg = c.avgRating ?? 0
          return (
            <div key={c.key} className="grid grid-cols-[8.5rem_1fr_auto] items-center gap-3 sm:grid-cols-[11rem_1fr_13rem]">
              <Chip card={card} />
              <div className="h-6 rounded bg-zinc-100 dark:bg-zinc-800" title={(c.ratingDistribution ?? []).map((n, i) => `${i + 1}★: ${n}`).join(' · ')}>
                <div className="flex h-6 items-center justify-end rounded bg-amber-400 pr-2 text-xs font-bold" style={{ width: `${(avg / 5) * 100}%` }}>
                  <span className="rounded bg-white/85 px-1 text-zinc-900">{avg.toFixed(1)}</span>
                </div>
              </div>
              <span className="text-xs font-medium text-red-700 dark:text-red-400">{weak.has(c.key) ? t.results.ratingLow : ''}</span>
            </div>
          )
        })}
      </div>
    </section>
  )
}

function Heatmap({ result, cards }: { result: MotivatorsResult; cards: Cards }) {
  const max = Math.max(1, ...result.cards.flatMap((c) => c.distribution))
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[34rem] border-separate border-spacing-0.5 text-sm">
        <thead>
          <tr>
            <th />
            {Array.from({ length: 10 }, (_, i) => (
              <th key={i} className="w-[8%] pb-1 text-center text-xs font-normal text-zinc-500">{i + 1}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {result.cards.map((c) => {
            const card = cards.get(c.key)!
            return (
              <tr key={c.key}>
                <th className="pr-2 text-left font-normal whitespace-nowrap"><Chip card={card} /></th>
                {c.distribution.map((n, i) => (
                  <td
                    key={i}
                    className="h-8 rounded text-center text-xs font-medium"
                    style={{
                      background: n ? card.accentColor : undefined,
                      opacity: n ? 0.25 + 0.75 * (n / max) : 1,
                    }}
                    title={`${card.name}, позиция ${i + 1}: ${n}`}
                  >
                    <span className={n ? 'text-zinc-900' : 'text-zinc-300 dark:text-zinc-700'}>{n || '·'}</span>
                  </td>
                ))}
              </tr>
            )
          })}
        </tbody>
        <tfoot>
          <tr>
            <td />
            <td colSpan={5} className="pt-1 text-xs text-zinc-500">← {t.results.lessImportant}</td>
            <td colSpan={5} className="pt-1 text-right text-xs text-zinc-500">{t.results.moreImportant} →</td>
          </tr>
        </tfoot>
      </table>
    </div>
  )
}

function ImpactTab({ result, cards }: { result: MotivatorsResult; cards: Cards }) {
  const painful = result.changes.find((c) => c.id === result.mostPainfulChangeId)
  const positive = result.changes.find((c) => c.id === result.mostPositiveChangeId)
  return (
    <div className="space-y-10">
      <p className="text-sm text-zinc-500">{t.results.impactHint}</p>

      {(painful || positive) && (
        <div className="grid gap-3 sm:grid-cols-2">
          {painful && (
            <div className="rounded-xl border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950/40">
              <div className="text-xs font-bold uppercase text-red-700 dark:text-red-400">{t.results.mostPainful}</div>
              <div className="mt-1 font-medium">«{painful.text}»</div>
              <div className="text-sm text-zinc-600 dark:text-zinc-400">{t.results.overall}: {signed(painful.overall)}</div>
            </div>
          )}
          {positive && (
            <div className="rounded-xl border border-green-200 bg-green-50 p-4 dark:border-green-900 dark:bg-green-950/40">
              <div className="text-xs font-bold uppercase text-green-700 dark:text-green-400">{t.results.mostPositive}</div>
              <div className="mt-1 font-medium">«{positive.text}»</div>
              <div className="text-sm text-zinc-600 dark:text-zinc-400">{t.results.overall}: {signed(positive.overall)}</div>
            </div>
          )}
        </div>
      )}

      {result.changes.length > 1 && (
        <section>
          <h2 className="mb-4 text-xl font-bold">{t.results.matrix}</h2>
          <div className="overflow-x-auto">
            <table className="min-w-full border-separate border-spacing-0.5 text-sm">
              <thead>
                <tr>
                  <th />
                  {result.cards.map((c) => (
                    <th key={c.key} className="h-28 align-bottom">
                      <div className="ml-auto mr-auto w-6 [writing-mode:vertical-rl] rotate-180 text-left text-xs font-normal whitespace-nowrap">
                        {cards.get(c.key)?.name}
                      </div>
                    </th>
                  ))}
                  <th className="px-2 align-bottom text-xs">{t.results.overall}</th>
                </tr>
              </thead>
              <tbody>
                {[...result.changes].sort((a, b) => a.overall - b.overall).map((ch) => (
                  <tr key={ch.id}>
                    <th className="max-w-48 pr-3 text-left font-normal">{ch.text}</th>
                    {ch.cards.map((ci) => (
                      <td key={ci.key} className="h-9 min-w-9 rounded text-center text-xs" style={{ background: impactColor(ci.index) }}
                        title={`${cards.get(ci.key)?.name}: ${signed(ci.index)}`}>
                        <span className="text-zinc-900">{ci.index === 0 ? '' : signed(ci.index)}</span>
                      </td>
                    ))}
                    <td className="rounded px-2 text-center font-bold" style={{ background: impactColor(ch.overall) }}>
                      <span className="text-zinc-900">{signed(ch.overall)}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {result.changes.map((ch) => (
        <section key={ch.id} className="panel">
          <div className="mb-4 flex flex-wrap items-baseline justify-between gap-2">
            <h3 className="text-lg font-bold">«{ch.text}»</h3>
            <span className="rounded-full px-3 py-1 text-sm font-bold text-zinc-900" style={{ background: impactColor(ch.overall) }}>
              {t.results.overall}: {signed(ch.overall)}
            </span>
          </div>
          <div className="space-y-1.5">
            {ch.cards.map((ci) => (
              <div key={ci.key} className="grid grid-cols-[8.5rem_1fr_3.5rem] items-center gap-3 text-sm sm:grid-cols-[11rem_1fr_3.5rem]">
                <Chip card={cards.get(ci.key)} />
                <ImpactBar up={ci.up} same={ci.neutral} down={ci.down} />
                <span className="text-right font-mono text-xs">{signed(ci.index)}</span>
              </div>
            ))}
          </div>
          <div className="mt-3 flex gap-4 text-xs text-zinc-500">
            <span><span className="inline-block h-2 w-3 rounded bg-green-500" /> ↑ {t.results.better}</span>
            <span><span className="inline-block h-2 w-3 rounded bg-zinc-300" /> — {t.results.same}</span>
            <span><span className="inline-block h-2 w-3 rounded bg-red-500" /> ↓ {t.results.worse}</span>
          </div>
        </section>
      ))}
    </div>
  )
}

function ImpactBar({ up, same, down }: { up: number; same: number; down: number }) {
  const n = Math.max(1, up + same + down)
  return (
    <div className="flex h-5 overflow-hidden rounded bg-zinc-100 text-[10px] font-bold text-white dark:bg-zinc-800" title={`↑${up} —${same} ↓${down}`}>
      {down > 0 && <div className="flex items-center justify-center bg-red-500" style={{ width: `${(down / n) * 100}%` }}>{down}</div>}
      {same > 0 && <div className="flex items-center justify-center bg-zinc-300 text-zinc-700" style={{ width: `${(same / n) * 100}%` }}>{same}</div>}
      {up > 0 && <div className="flex items-center justify-center bg-green-500" style={{ width: `${(up / n) * 100}%` }}>{up}</div>}
    </div>
  )
}

function ParticipantsTab({ result, cards }: { result: MotivatorsResult; cards: Cards }) {
  const participants = result.participants ?? []
  const keys = result.cards.map((c) => c.key)
  return (
    <div className="space-y-10">
      <div className="overflow-x-auto">
        <table className="min-w-full border-separate border-spacing-0.5 text-sm">
          <thead>
            <tr>
              <th className="text-left text-xs font-normal text-zinc-500">{t.results.participant}</th>
              {keys.map((k) => (
                <th key={k} className="h-28 align-bottom">
                  <div className="ml-auto mr-auto w-6 [writing-mode:vertical-rl] rotate-180 text-left text-xs font-normal whitespace-nowrap">{cards.get(k)?.name}</div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {participants.map((p, pi) => (
              <tr key={pi}>
                <th className="pr-3 text-left font-medium whitespace-nowrap">{p.name}</th>
                {keys.map((k) => {
                  const rank = p.ranking.indexOf(k) + 1
                  const card = cards.get(k)!
                  return (
                    <td key={k} className="h-9 min-w-9 rounded text-center text-xs font-bold text-zinc-900"
                      style={{ background: card.accentColor, opacity: 0.15 + 0.85 * (rank / 10) }}>
                      {rank}
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
        <p className="hint">10 — {t.results.moreImportant.toLowerCase()}, 1 — {t.results.lessImportant.toLowerCase()}</p>
      </div>

      {participants.some((p) => p.ratings) && (
        <section>
          <h3 className="mb-3 text-lg font-bold">{t.results.ratingTitle}</h3>
          <div className="overflow-x-auto">
            <table className="min-w-full border-separate border-spacing-0.5 text-sm">
              <thead>
                <tr>
                  <th className="text-left text-xs font-normal text-zinc-500">{t.results.participant}</th>
                  {keys.map((k) => (
                    <th key={k} className="h-28 align-bottom">
                      <div className="ml-auto mr-auto w-6 [writing-mode:vertical-rl] rotate-180 text-left text-xs font-normal whitespace-nowrap">{cards.get(k)?.name}</div>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {participants.map((p, pi) => (
                  <tr key={pi}>
                    <th className="pr-3 text-left font-medium whitespace-nowrap">{p.name}</th>
                    {keys.map((k) => {
                      const v = p.ratings?.[k]
                      return (
                        <td key={k} className="h-9 min-w-9 rounded bg-amber-400 text-center text-xs font-bold text-zinc-900"
                          style={{ opacity: v ? 0.15 + 0.85 * (v / 5) : 0.1 }}>
                          {v ?? '—'}
                        </td>
                      )
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {result.changes.map((ch) => (
        <section key={ch.id}>
          <h3 className="mb-3 text-lg font-bold">{t.results.whoMarked}: «{ch.text}»</h3>
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <tbody>
                {participants.map((p, pi) => {
                  const marks = p.impact?.[ch.id] ?? {}
                  const up = keys.filter((k) => marks[k] > 0)
                  const down = keys.filter((k) => marks[k] < 0)
                  return (
                    <tr key={pi} className="border-b border-zinc-100 dark:border-zinc-800">
                      <th className="py-2 pr-4 text-left align-top font-medium whitespace-nowrap">{p.name}</th>
                      <td className="py-2 pr-4 align-top">
                        <div className="flex flex-wrap gap-x-3 gap-y-1 text-green-700 dark:text-green-400">
                          {up.length ? up.map((k) => <span key={k}>↑ {cards.get(k)?.name}</span>) : <span className="text-zinc-400">↑ —</span>}
                        </div>
                      </td>
                      <td className="py-2 align-top">
                        <div className="flex flex-wrap gap-x-3 gap-y-1 text-red-700 dark:text-red-400">
                          {down.length ? down.map((k) => <span key={k}>↓ {cards.get(k)?.name}</span>) : <span className="text-zinc-400">↓ —</span>}
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </div>
  )
}
