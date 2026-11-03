import { SortableList } from '@/components/SortableList'
import { uuid } from '@/lib/storage'
import { t } from '@/i18n'

export interface ChangeDraft {
  /** Локальный ключ для dnd; серверный id — отдельно. */
  key: string
  id?: string
  text: string
}

export const newChangeKey = () => uuid()
const MAX = 5

/** Список изменений: добавление, удаление, перетаскивание. locked — только правка текста. */
export function ChangesEditor({ value, onChange, locked }: { value: ChangeDraft[]; onChange: (v: ChangeDraft[]) => void; locked?: boolean }) {
  const byKey = new Map(value.map((c) => [c.key, c]))
  return (
    <div>
      <SortableList
        items={value.map((c) => c.key)}
        disabled={locked}
        onChange={(keys) => onChange(keys.map((k) => byKey.get(k)!))}
        className="space-y-2"
        renderItem={(key, i, { isDragging, ...handle }) => {
          const c = byKey.get(key)!
          return (
            <div className={`flex items-center gap-2 rounded-lg bg-white dark:bg-zinc-900 ${isDragging ? 'shadow-lg' : ''}`}>
              {!locked && (
                <button type="button" {...handle} className="cursor-grab touch-none px-1 text-xl text-zinc-400" aria-label="Перетащить">
                  ⠿
                </button>
              )}
              <span className="w-5 text-sm text-zinc-400">{i + 1}.</span>
              <input
                className="input"
                required
                maxLength={300}
                placeholder={t.wizard.changePlaceholder}
                value={c.text}
                onChange={(e) => onChange(value.map((x) => (x.key === key ? { ...x, text: e.target.value } : x)))}
              />
              {!locked && value.length > 1 && (
                <button
                  type="button"
                  className="px-2 text-xl text-zinc-400 hover:text-red-600"
                  aria-label={t.wizard.removeChange}
                  onClick={() => onChange(value.filter((x) => x.key !== key))}
                >
                  ×
                </button>
              )}
            </div>
          )
        }}
      />
      {locked ? (
        <p className="hint">{t.wizard.changesLocked}</p>
      ) : (
        value.length < MAX && (
          <button type="button" className="btn-ghost mt-2 px-2" onClick={() => onChange([...value, { key: newChangeKey(), text: '' }])}>
            {t.wizard.addChange}
          </button>
        )
      )}
    </div>
  )
}
