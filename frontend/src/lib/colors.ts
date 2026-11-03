/**
 * Цвет текста на фоне accentColor по относительной яркости (WCAG).
 * Порог 0.55 — та же формула, что в backend (Colors.TextOn).
 */
export function textOn(hex: string): string {
  const h = hex.replace('#', '')
  const lin = (i: number) => {
    const c = parseInt(h.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4)
  }
  const l = 0.2126 * lin(0) + 0.7152 * lin(2) + 0.0722 * lin(4)
  return l > 0.55 ? '#1A1A1A' : '#FFFFFF'
}

export const traffic = {
  green: '#22A559',
  yellow: '#F2C230',
  red: '#E5484D',
  none: '#BBBBBB',
} as const

export const trafficColor = (c?: string | null) =>
  c === 'green' ? traffic.green : c === 'yellow' ? traffic.yellow : c === 'red' ? traffic.red : traffic.none

/** Индекс влияния −1..1 → красный ← белый → зелёный. */
export function impactColor(index: number): string {
  const v = Math.max(-1, Math.min(1, index))
  const mix = (a: number[], b: number[], t: number) => a.map((x, i) => Math.round(x + (b[i] - x) * t))
  const neutral = [245, 245, 245]
  const rgb = v < 0 ? mix(neutral, [229, 72, 77], -v) : mix(neutral, [34, 165, 89], v)
  return `rgb(${rgb.join(',')})`
}

export const trendArrow = (t?: number | null) => (t === 1 ? '↑' : t === -1 ? '↓' : t === 0 ? '→' : '')
