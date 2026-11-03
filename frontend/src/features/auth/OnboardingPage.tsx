import { useState } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { keys, useMe } from '@/api/hooks'
import type { User } from '@/api/types'
import { Page } from '@/components/Layout'
import { t } from '@/i18n'

export default function OnboardingPage() {
  const { data: me } = useMe()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [name, setName] = useState(me?.displayName ?? '')
  const [consent, setConsent] = useState(false)
  const returnUrl = params.get('returnUrl')
  const target = returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/app'

  const save = useMutation({
    mutationFn: () => api.patch<User>('/me', { displayName: name, consentVersion: me!.currentConsentVersion }),
    onSuccess: (user) => {
      qc.setQueryData(keys.me, user)
      navigate(target, { replace: true })
    },
  })

  if (me && !me.needsOnboarding) return <Navigate to={target} replace />

  return (
    <Page>
      <div className="mx-auto max-w-md">
        <h1 className="mb-6 text-3xl font-bold">{t.auth.onboardingTitle}</h1>
        <form
          className="panel space-y-5"
          onSubmit={(e) => {
            e.preventDefault()
            if (consent) save.mutate()
          }}
        >
          <div>
            <label className="label" htmlFor="name">{t.auth.onboardingName}</label>
            <input id="name" className="input" maxLength={100} value={name} onChange={(e) => setName(e.target.value)} autoComplete="name" />
            <p className="hint">{t.auth.onboardingNameHint}</p>
          </div>
          <label className="flex items-start gap-3 text-sm">
            <input type="checkbox" className="mt-1 h-5 w-5 shrink-0 accent-brand" checked={consent} onChange={(e) => setConsent(e.target.checked)} required />
            <span>
              {t.auth.onboardingConsentPrefix}
              <Link to="/terms" target="_blank" className="text-brand underline dark:text-violet-300">{t.auth.onboardingConsentOffer}</Link>
              {t.auth.onboardingConsentMiddle}
              <Link to="/privacy" target="_blank" className="text-brand underline dark:text-violet-300">{t.auth.onboardingConsentPolicy}</Link>.
            </span>
          </label>
          {save.isError && <p className="error-text">{errorMessage(save.error)}</p>}
          <button className="btn-primary w-full" disabled={!consent || save.isPending}>{t.auth.onboardingContinue}</button>
        </form>
      </div>
    </Page>
  )
}
