import { useEffect, useState } from 'react'

export function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => typeof window !== 'undefined' && window.matchMedia(query).matches)
  useEffect(() => {
    const mq = window.matchMedia(query)
    const on = () => setMatches(mq.matches)
    mq.addEventListener('change', on)
    return () => mq.removeEventListener('change', on)
  }, [query])
  return matches
}

/**
 * Горизонтальный ряд из 10 full-карточек — от 1200px: карточка не уже ~110px, формулировки
 * читаются без мелкого шрифта. Уже — вертикальный список compact-карточек.
 */
export const useIsDesktop = () => useMediaQuery('(min-width: 1200px)')
