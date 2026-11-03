import { useState } from 'react'
import { Navigate, useSearchParams } from 'react-router'
import { useMutation } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { useMe } from '@/api/hooks'
import { Page } from '@/components/Layout'
import { t } from '@/i18n'

export function LoginForm({ returnUrl, compact }: { returnUrl?: string | null; compact?: boolean }) {
  const [email, setEmail] = useState('')
  const send = useMutation({
    mutationFn: () => api.post('/auth/request-link', { email, returnUrl: returnUrl ?? undefined }),
  })

  if (send.isSuccess) {
    return (
      <div className="panel">
        <h2 className="mb-2 text-xl font-bold">{t.auth.sentTitle}</h2>
        <p className="mb-4 text-zinc-600 dark:text-zinc-300">{t.auth.sentText}</p>
        <button className="btn-ghost px-0" onClick={() => send.reset()}>{t.auth.sentAgain}</button>
      </div>
    )
  }

  return (
    <form
      className={compact ? 'flex flex-col gap-2 sm:flex-row' : 'panel space-y-4'}
      onSubmit={(e) => {
        e.preventDefault()
        send.mutate()
      }}
    >
      <div className={compact ? 'flex-1' : ''}>
        {!compact && <label className="label" htmlFor="email">{t.auth.email}</label>}
        <input
          id="email"
          type="email"
          required
          autoComplete="email"
          inputMode="email"
          className="input"
          placeholder={t.auth.emailPlaceholder}
          aria-label={t.auth.email}
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
      </div>
      <button className="btn-primary" disabled={send.isPending}>
        {compact ? t.landing.cta : t.auth.sendLink}
      </button>
      {send.isError && <p className="error-text">{errorMessage(send.error)}</p>}
    </form>
  )
}

export default function LoginPage() {
  const [params] = useSearchParams()
  const { data: me } = useMe()
  const returnUrl = params.get('returnUrl')
  if (me) return <Navigate to={returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/app'} replace />

  return (
    <Page>
      <div className="mx-auto max-w-md">
        <h1 className="mb-2 text-3xl font-bold">{t.auth.loginTitle}</h1>
        <p className="mb-6 text-zinc-600 dark:text-zinc-300">{t.auth.loginText}</p>
        <LoginForm returnUrl={returnUrl} />
      </div>
    </Page>
  )
}
