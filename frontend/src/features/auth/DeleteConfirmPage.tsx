import { Link, useSearchParams } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { keys } from '@/api/hooks'
import { Page } from '@/components/Layout'
import { t } from '@/i18n'

export default function DeleteConfirmPage() {
  const [params] = useSearchParams()
  const token = params.get('token')
  const qc = useQueryClient()
  const del = useMutation({
    mutationFn: () => api.post('/me/delete-confirm', { token }),
    onSuccess: () => {
      qc.setQueryData(keys.me, null)
      qc.clear()
    },
  })

  return (
    <Page>
      <div className="panel mx-auto max-w-md">
        <h1 className="mb-3 text-2xl font-bold">{t.settings.deleteConfirmTitle}</h1>
        {del.isSuccess ? (
          <>
            <p className="mb-5">{t.settings.deleted}</p>
            <Link to="/" className="btn-secondary">{t.common.toHome}</Link>
          </>
        ) : (
          <>
            <p className="mb-5 text-zinc-600 dark:text-zinc-300">{t.settings.deleteConfirmText}</p>
            <button className="btn-danger w-full" disabled={!token || del.isPending} onClick={() => del.mutate()}>
              {t.settings.deleteConfirmButton}
            </button>
            {del.isError && <p className="error-text mt-3">{errorMessage(del.error)}</p>}
          </>
        )}
      </div>
    </Page>
  )
}
