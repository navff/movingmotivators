import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from './client'
import type { Catalog, Pricing, SiteInfo, User } from './types'

export const keys = {
  me: ['me'] as const,
  catalog: ['catalog'] as const,
  pricing: ['pricing'] as const,
  site: ['site'] as const,
  teams: ['teams'] as const,
  team: (id: string) => ['teams', id] as const,
  survey: (id: string) => ['surveys', id] as const,
  results: (id: string) => ['surveys', id, 'results'] as const,
}

/** Текущий пользователь; null — не вошёл. */
export function useMe() {
  return useQuery({
    queryKey: keys.me,
    queryFn: async () => {
      try {
        return await api.get<User>('/me')
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return null
        throw e
      }
    },
    staleTime: 60_000,
  })
}

export const useCatalog = () =>
  useQuery({ queryKey: keys.catalog, queryFn: () => api.get<Catalog>('/catalog'), staleTime: Infinity })

export const usePricing = () =>
  useQuery({ queryKey: keys.pricing, queryFn: () => api.get<Pricing>('/pricing'), staleTime: 5 * 60_000 })

export const useSite = () =>
  useQuery({ queryKey: keys.site, queryFn: () => api.get<SiteInfo>('/site'), staleTime: Infinity })
