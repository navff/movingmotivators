import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, errorMessage } from '@/api/client'
import { keys } from '@/api/hooks'
import type { CheckoutSummary, PaymentMethod, Survey } from '@/api/types'
import { Page } from '@/components/Layout'
import { ErrorBox, Spinner } from '@/components/ui'
import { fmt, t } from '@/i18n'

const methods: { id: PaymentMethod; label: string; icon: string }[] = [
  { id: 'card', label: t.checkout.card, icon: '💳' },
  { id: 'sbp', label: t.checkout.sbp, icon: '⚡' },
  { id: 'sberpay', label: t.checkout.sberpay, icon: '🟢' },
]

/**
 * Экран оплаты (имитация, §7.2): только выбор способа — никаких полей карты/телефона.
 * «Оплатить» фиксирует payment_clicked и запускает опрос бесплатно.
 */
export default function CheckoutPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [method, setMethod] = useState<PaymentMethod | null>(null)
  const viewed = useRef(false)

  const view = useMutation({ mutationFn: () => api.post<CheckoutSummary>(`/surveys/${id}/checkout/view`) })
  useEffect(() => {
    // StrictMode вызывает эффект дважды — фиксируем просмотр один раз
    if (viewed.current) return
    viewed.current = true
    view.mutate()
  }, [view])

  const select = useMutation({ mutationFn: (m: PaymentMethod) => api.post(`/surveys/${id}/checkout/method`, { method: m }) })
  const pay = useMutation({
    mutationFn: () => api.post<{ status: string; confirmationUrl?: string; survey: Survey }>(`/surveys/${id}/checkout`, { method }),
    onSuccess: (r) => {
      qc.setQueryData(keys.survey(id), r.survey)
      qc.invalidateQueries({ queryKey: keys.team(r.survey.teamId) })
      if (r.confirmationUrl) window.location.href = r.confirmationUrl
      else navigate(`/app/surveys/${id}/share?launched=free`, { replace: true })
    },
  })

  if (view.isError) {
    const launched = view.error instanceof ApiError && view.error.errorCode === 'invalid_state'
    return (
      <Page>
        {launched ? (
          <div className="panel">
            <p className="mb-4">{t.checkout.alreadyLaunched}</p>
            <Link to={`/app/surveys/${id}`} className="btn-primary">{t.share.toResults}</Link>
          </div>
        ) : (
          <ErrorBox error={view.error} />
        )}
      </Page>
    )
  }
  const s = view.data
  if (!s) return <Spinner />

  return (
    <Page>
      <h1 className="mb-6 text-3xl font-bold">{t.checkout.title}</h1>
      <div className="panel space-y-6">
        <div>
          <p className="text-zinc-600 dark:text-zinc-300">
            {fmt(t.checkout.summary, { type: t.types[s.surveyType], team: s.teamName, n: s.participants })}
          </p>
          <p className="mt-1 text-sm text-zinc-500">«{s.surveyTitle}»</p>
          <div className="mt-4 flex items-baseline justify-between border-t border-zinc-200 pt-4 dark:border-zinc-800">
            <span className="text-zinc-600 dark:text-zinc-300">{t.checkout.amount}</span>
            <span className="font-slab text-3xl font-bold">{s.amountRub} ₽</span>
          </div>
        </div>

        <fieldset>
          <legend className="label">{t.checkout.method}</legend>
          <div className="grid gap-2 sm:grid-cols-3">
            {methods.map((m) => (
              <label
                key={m.id}
                className={`flex cursor-pointer items-center gap-3 rounded-lg border-2 p-4 ${method === m.id ? 'border-brand bg-brand-50 dark:bg-zinc-800' : 'border-zinc-200 dark:border-zinc-700'}`}
              >
                <input
                  type="radio"
                  name="method"
                  className="h-4 w-4 accent-brand"
                  checked={method === m.id}
                  onChange={() => {
                    setMethod(m.id)
                    select.mutate(m.id)
                  }}
                />
                <span aria-hidden>{m.icon}</span>
                <span className="font-medium">{m.label}</span>
              </label>
            ))}
          </div>
        </fieldset>

        <p className="text-sm text-zinc-600 dark:text-zinc-300">{fmt(t.checkout.receipt, { email: s.email })}</p>

        {pay.isError && <p className="error-text">{errorMessage(pay.error)}</p>}
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
          <button type="button" className="btn-secondary" onClick={() => navigate(-1)}>{t.common.back}</button>
          <button className="btn-primary" disabled={!method || pay.isPending} onClick={() => pay.mutate()} title={!method ? t.checkout.chooseMethod : undefined}>
            {fmt(t.checkout.pay, { price: s.amountRub })}
          </button>
        </div>

        <p className="border-t border-zinc-200 pt-4 text-xs text-zinc-500 dark:border-zinc-800">
          {s.legalName}, ИНН {s.legalInn}. {t.checkout.offerPrefix}
          <Link to="/terms" target="_blank" className="underline">{t.checkout.offer}</Link>.
        </p>
      </div>
    </Page>
  )
}
