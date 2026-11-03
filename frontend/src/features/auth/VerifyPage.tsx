import { Link, useNavigate, useSearchParams } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import type { User } from '@/api/types'
import { keys } from '@/api/hooks'
import { Page } from '@/components/Layout'
import { t } from '@/i18n'

const safe = (url?: string | null) => (url && url.startsWith('/') && !url.startsWith('//') ? url : null)

/**
 * GET этой страницы токен не тратит (почтовые сканеры открывают ссылки) —
 * вход только по нажатию кнопки.
 */
export default function VerifyPage() {
  const [params] = useSearchParams()
  const token = params.get('token')
  const navigate = useNavigate()
  const qc = useQueryClient()

  const verify = useMutation({
    mutationFn: () => api.post<{ user: User; isNew: boolean; returnUrl?: string }>('/auth/verify', { token }),
    onSuccess: ({ user, returnUrl }) => {
      qc.setQueryData(keys.me, user)
      const target = safe(returnUrl) ?? '/app'
      navigate(user.needsOnboarding ? `/onboarding?returnUrl=${encodeURIComponent(target)}` : target, { replace: true })
    },
  })

  return (
    <Page>
      <div className="panel mx-auto max-w-md text-center">
        <h1 className="mb-3 text-2xl font-bold">{t.auth.verifyTitle}</h1>
        {!token ? (
          <>
            <p className="mb-5 text-zinc-600">{t.auth.verifyNoToken}</p>
            <Link to="/login" className="btn-primary">{t.auth.requestNew}</Link>
          </>
        ) : (
          <>
            <p className="mb-5 text-zinc-600 dark:text-zinc-300">{t.auth.verifyText}</p>
            <button className="btn-primary w-full" onClick={() => verify.mutate()} disabled={verify.isPending}>
              {t.auth.verifyButton}
            </button>
            {verify.isError && (
              <div className="mt-4 space-y-3">
                <p className="error-text">{errorMessage(verify.error)}</p>
                <Link to="/login" className="btn-ghost">{t.auth.requestNew}</Link>
              </div>
            )}
          </>
        )}
      </div>
    </Page>
  )
}
