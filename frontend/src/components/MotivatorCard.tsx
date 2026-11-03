import type { CSSProperties, ReactNode } from 'react'
import type { MotivatorCardInfo } from '@/api/types'
import { textOn } from '@/lib/colors'
import { hyphenate } from '@/lib/hyphenate'

// SVG (если положить {key}.svg из Figma) приоритетнее PNG.
const files = import.meta.glob<string>('../assets/motivators/*.{png,svg}', {
  eager: true,
  query: '?url',
  import: 'default',
})

const illustrations: Record<string, string> = {}
for (const [path, url] of Object.entries(files)) {
  const [, name, ext] = path.match(/\/([\w-]+)\.(png|svg)$/) ?? []
  if (!name) continue
  if (ext === 'svg' || !illustrations[name]) illustrations[name] = url
}

export const illustrationUrl = (key: string) => illustrations[key]

interface Props {
  card: MotivatorCardInfo
  mode?: 'full' | 'compact' | 'chip'
  showEnglish?: boolean
  className?: string
  style?: CSSProperties
  /** Справа в compact-плашке (кнопки ↑/↓, отметки влияния). */
  actions?: ReactNode
  /** Порядковый номер / бейдж в углу. */
  badge?: ReactNode
}

export function MotivatorCard({ card, mode = 'full', showEnglish, className = '', style, actions, badge }: Props) {
  const fg = textOn(card.accentColor)
  const img = illustrations[card.key]

  if (mode === 'chip') {
    return (
      <span className={`inline-flex items-center gap-1.5 whitespace-nowrap ${className}`} style={style}>
        <span className="inline-block h-2.5 w-2.5 shrink-0 rounded-full" style={{ background: card.accentColor }} aria-hidden />
        {card.name}
      </span>
    )
  }

  if (mode === 'compact') {
    return (
      <div
        className={`flex items-stretch overflow-hidden rounded-lg border border-zinc-300 bg-white text-zinc-900 shadow-sm ${className}`}
        style={style}
      >
        <div className="w-2 shrink-0" style={{ background: card.accentColor }} aria-hidden />
        <div className="flex min-w-0 flex-1 items-center gap-3 px-3 py-2">
          {badge}
          {img && <img src={img} alt="" loading="lazy" className="h-10 w-10 shrink-0 object-contain" />}
          <div className="min-w-0 flex-1">
            <div className="font-slab text-sm font-bold uppercase tracking-wide">{card.name}</div>
            <div className="text-xs leading-snug text-zinc-600">{card.statement}</div>
          </div>
        </div>
        {actions && <div className="flex shrink-0 items-center gap-1 pr-2">{actions}</div>}
      </div>
    )
  }

  return (
    <div className={`mm-card relative ${className}`} style={style}>
      <div className="mm-card-header" style={{ background: card.accentColor, color: fg }}>
        {card.name}
      </div>
      <div className="mm-card-ruler" aria-hidden />
      {showEnglish && card.englishName && (
        <div className="text-center text-[10px] uppercase tracking-widest text-zinc-400">{card.englishName}</div>
      )}
      {badge && <div className="absolute top-9 right-2">{badge}</div>}
      <div className="flex flex-1 items-center justify-center px-3 py-2" style={{ minHeight: '5.5rem' }}>
        {img && <img src={img} alt="" loading="lazy" className="max-h-24 max-w-[85%] object-contain" />}
      </div>
      <div className="mm-bubble">{hyphenate(card.statement)}</div>
    </div>
  )
}
