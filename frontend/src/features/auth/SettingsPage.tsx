import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { keys, useMe } from '@/api/hooks'
import type { User } from '@/api/types'
import { Page } from '@/components/Layout'
import { ConfirmModal, PageTitle } from '@/components/ui'
import { t } from '@/i18n'

export default function SettingsPage() {
  const { data: me } = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [name, setName] = useState(me?.displayName ?? '')
  const [confirmDelete, setConfirmDelete] = useState(false)

  const save = useMutation({
    mutationFn: () => api.patch<User>('/me', { displayName: name }),
    onSuccess: (u) => qc.setQueryData(keys.me, u),
  })
  const logoutAll = useMutation({
    mutationFn: () => api.post('/auth/logout-all'),
    onSuccess: () => {
      qc.setQueryData(keys.me, null)
      navigate('/login')
    },
  })
  const requestDelete = useMutation({ mutationFn: () => api.post('/me/delete-request'), onSuccess: () => setConfirmDelete(false) })

  if (!me) return null

  return (
    <Page>
      <PageTitle>{t.settings.title}</PageTitle>
      <div className="space-y-6">
        <section className="panel">
          <h2 className="mb-4 text-lg font-bold">{t.settings.profile}</h2>
          <form
            className="space-y-4"
            onSubmit={(e) => {
              e.preventDefault()
              save.mutate()
            }}
          >
            <div>
              <span className="label">{t.settings.email}</span>
              <p>{me.email}</p>
            </div>
            <div>
              <label className="label" htmlFor="name">{t.settings.name}</label>
              <input id="name" className="input" maxLength={100} value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="flex items-center gap-3">
              <button className="btn-primary" disabled={save.isPending}>{t.common.save}</button>
              {save.isSuccess && <span className="text-sm text-green-700">✓ {t.common.saved}</span>}
              {save.isError && <span className="error-text">{errorMessage(save.error)}</span>}
            </div>
          </form>
        </section>

        <section className="panel">
          <h2 className="mb-4 text-lg font-bold">{t.settings.sessions}</h2>
          <button className="btn-secondary" onClick={() => logoutAll.mutate()} disabled={logoutAll.isPending}>
            {t.settings.logoutAll}
          </button>
        </section>

        <section className="panel border-red-200 dark:border-red-900">
          <h2 className="mb-2 text-lg font-bold text-red-700 dark:text-red-400">{t.settings.danger}</h2>
          <p className="mb-4 text-sm text-zinc-600 dark:text-zinc-300">{t.settings.dangerText}</p>
          {requestDelete.isSuccess ? (
            <p className="text-sm font-medium">{t.settings.deleteSent}</p>
          ) : (
            <button className="btn-danger" onClick={() => setConfirmDelete(true)}>{t.settings.requestDelete}</button>
          )}
          {requestDelete.isError && <p className="error-text mt-2">{errorMessage(requestDelete.error)}</p>}
        </section>
      </div>
      <ConfirmModal
        open={confirmDelete}
        title={t.settings.danger}
        text={t.settings.dangerText}
        confirmLabel={t.settings.requestDelete}
        danger
        busy={requestDelete.isPending}
        onConfirm={() => requestDelete.mutate()}
        onClose={() => setConfirmDelete(false)}
      />
    </Page>
  )
}
