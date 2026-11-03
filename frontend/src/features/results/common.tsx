import type { Catalog, CommentItem, DiscussionPrompt, MotivatorCardInfo } from '@/api/types'
import { MotivatorCard } from '@/components/MotivatorCard'
import { formatDate, t } from '@/i18n'

export function cardMap(catalog: Catalog) {
  return new Map<string, MotivatorCardInfo>(catalog.motivators.map((m) => [m.key, m]))
}

export function Chip({ card }: { card?: MotivatorCardInfo }) {
  return card ? <MotivatorCard card={card} mode="chip" /> : null
}

export function Comments({ items, anonymous, label }: { items: CommentItem[]; anonymous: boolean; label?: (c: CommentItem) => string | undefined }) {
  if (items.length === 0) return <p className="text-zinc-500">{t.results.noComments}</p>
  return (
    <div className="space-y-3">
      {anonymous && <p className="text-sm text-zinc-500">{t.results.commentsAnonNote}</p>}
      {items.map((c, i) => (
        <blockquote key={i} className="panel py-4">
          {label?.(c) && <div className="mb-1 text-xs font-medium uppercase tracking-wide text-zinc-500">{label(c)}</div>}
          <p className="whitespace-pre-wrap">{c.text}</p>
          {(c.name || c.submittedOn) && (
            <footer className="mt-2 text-sm text-zinc-500">
              {c.name}
              {c.submittedOn && ` · ${formatDate(c.submittedOn)}`}
            </footer>
          )}
        </blockquote>
      ))}
    </div>
  )
}

export function Prompts({ prompts }: { prompts: DiscussionPrompt[] }) {
  if (prompts.length === 0) return <p className="text-zinc-500">{t.results.noPrompts}</p>
  return (
    <div className="space-y-3">
      {prompts.map((p, i) => (
        <div
          key={i}
          className={`rounded-xl border p-4 ${p.priority === 'high'
            ? 'border-red-300 bg-red-50 dark:border-red-900 dark:bg-red-950/50'
            : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'}`}
        >
          {p.priority === 'high' && (
            <div className="mb-1 text-xs font-bold uppercase tracking-wide text-red-700 dark:text-red-400">⚠ {t.results.priorityHigh}</div>
          )}
          <p className="font-medium">{p.text}</p>
          {p.questions && p.questions.length > 0 && (
            <ul className="mt-2 list-disc space-y-1 pl-5 text-zinc-700 dark:text-zinc-300">
              {p.questions.map((q) => <li key={q}>{q}</li>)}
            </ul>
          )}
        </div>
      ))}
    </div>
  )
}

export const signed = (v: number) => (v > 0 ? `+${v.toFixed(2)}` : v < 0 ? `−${Math.abs(v).toFixed(2)}` : '0')
