import type { MotivatorCardInfo } from '@/api/types'
import { MotivatorCard } from '@/components/MotivatorCard'
import { move, SortableList } from '@/components/SortableList'
import { t } from '@/i18n'
import { useIsDesktop } from './useMediaQuery'

/** Формулировки на карточках звучат как утверждения о текущей работе — поясняем, что оценивается важность. */
function RankNote() {
  return (
    <div className="mb-3 rounded-lg border-l-4 border-brand bg-brand-50 p-3 dark:bg-zinc-800">
      <div className="font-bold">{t.respond.rankNoteTitle}</div>
      <p className="text-sm text-zinc-700 dark:text-zinc-300">{t.respond.rankNoteText}</p>
    </div>
  )
}

/**
 * ranking: [наименее важная … самая важная].
 * Desktop — горизонтальный ряд full-карточек (слева менее важно).
 * Mobile — вертикальный список compact (сверху самое важное), + кнопки ↑/↓.
 */
export function RankingStep({
  cards, ranking, onChange, showEnglish,
}: {
  cards: Map<string, MotivatorCardInfo>
  ranking: string[]
  onChange: (r: string[]) => void
  showEnglish?: boolean
}) {
  const desktop = useIsDesktop()

  if (desktop) {
    return (
      <div>
        <RankNote />
        <p className="mb-4 text-zinc-600 dark:text-zinc-300">{t.respond.rankTextDesktop}</p>
        <div className="mb-2 flex justify-between text-sm font-medium text-zinc-500">
          <span>← {t.results.lessImportant}</span>
          <span>{t.results.moreImportant} →</span>
        </div>
        <SortableList
          items={ranking}
          direction="horizontal"
          onChange={onChange}
          className="grid grid-cols-10 gap-2"
          renderItem={(key, i, { isDragging, ...handle }) => (
            <div className="flex h-full flex-col">
              <div
                {...handle}
                className={`flex-1 cursor-grab touch-none select-none active:cursor-grabbing ${isDragging ? 'scale-105 shadow-2xl' : ''}`}
                aria-label={`${cards.get(key)!.name}, позиция ${i + 1} из ${ranking.length}`}
              >
                <MotivatorCard card={cards.get(key)!} showEnglish={showEnglish} className="h-full" />
              </div>
              <div className="mt-1 flex justify-center gap-1">
                <button type="button" className="rounded px-2 py-0.5 text-zinc-500 hover:bg-zinc-200 disabled:opacity-30 dark:hover:bg-zinc-800"
                  disabled={i === 0} onClick={() => onChange(move(ranking, i, -1))} aria-label={t.respond.moveLeft}>←</button>
                <button type="button" className="rounded px-2 py-0.5 text-zinc-500 hover:bg-zinc-200 disabled:opacity-30 dark:hover:bg-zinc-800"
                  disabled={i === ranking.length - 1} onClick={() => onChange(move(ranking, i, 1))} aria-label={t.respond.moveRight}>→</button>
              </div>
            </div>
          )}
        />
      </div>
    )
  }

  // на мобиле отображаем от самого важного
  const display = [...ranking].reverse()
  const setDisplay = (d: string[]) => onChange([...d].reverse())

  return (
    <div>
      <RankNote />
      <p className="mb-3 text-zinc-600 dark:text-zinc-300">{t.respond.rankTextMobile}</p>
      <div className="mb-1 text-xs font-bold uppercase tracking-wide text-zinc-500">↑ {t.results.moreImportant}</div>
      <SortableList
        items={display}
        onChange={setDisplay}
        className="space-y-2"
        renderItem={(key, i, { isDragging, ...handle }) => (
          <MotivatorCard
            card={cards.get(key)!}
            mode="compact"
            className={isDragging ? 'shadow-xl ring-2 ring-brand' : ''}
            badge={
              <span {...handle} className="flex h-8 w-8 shrink-0 cursor-grab touch-none items-center justify-center rounded-full bg-zinc-100 text-sm font-bold text-zinc-600"
                aria-label={`${cards.get(key)!.name}: перетащить, позиция ${i + 1}`}>
                {i + 1}
              </span>
            }
            actions={
              <div className="flex flex-col">
                <button type="button" className="flex h-8 w-9 items-center justify-center rounded text-lg text-zinc-600 active:bg-zinc-200 disabled:opacity-25"
                  disabled={i === 0} onClick={() => setDisplay(move(display, i, -1))} aria-label={`${t.respond.moveUp}: ${cards.get(key)!.name}`}>▲</button>
                <button type="button" className="flex h-8 w-9 items-center justify-center rounded text-lg text-zinc-600 active:bg-zinc-200 disabled:opacity-25"
                  disabled={i === display.length - 1} onClick={() => setDisplay(move(display, i, 1))} aria-label={`${t.respond.moveDown}: ${cards.get(key)!.name}`}>▼</button>
              </div>
            }
          />
        )}
      />
      <div className="mt-1 text-xs font-bold uppercase tracking-wide text-zinc-500">↓ {t.results.lessImportant}</div>
    </div>
  )
}
