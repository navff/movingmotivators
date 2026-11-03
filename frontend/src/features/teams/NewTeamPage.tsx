import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, errorMessage } from '@/api/client'
import { keys } from '@/api/hooks'
import type { Team } from '@/api/types'
import { Page } from '@/components/Layout'
import { PageTitle } from '@/components/ui'
import { t } from '@/i18n'

export default function NewTeamPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [size, setSize] = useState('')

  const create = useMutation({
    mutationFn: () => api.post<Team>('/teams', { name: name.trim(), size: Number(size) }),
    onSuccess: (team) => {
      qc.invalidateQueries({ queryKey: keys.teams })
      navigate(`/app/teams/${team.id}/surveys/new`)
    },
  })

  return (
    <Page>
      <PageTitle>{t.teams.newTitle}</PageTitle>
      <form
        className="panel space-y-5"
        onSubmit={(e) => {
          e.preventDefault()
          create.mutate()
        }}
      >
        <div>
          <label className="label" htmlFor="name">{t.teams.name}</label>
          <input id="name" className="input" required maxLength={100} autoFocus placeholder={t.teams.namePlaceholder}
            value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="size">{t.teams.size}</label>
          <input id="size" className="input max-w-40" type="number" inputMode="numeric" required min={2} max={200}
            value={size} onChange={(e) => setSize(e.target.value)} />
          <p className="hint">{t.teams.sizeHint}</p>
        </div>
        {create.isError && <p className="error-text">{errorMessage(create.error)}</p>}
        <button className="btn-primary" disabled={create.isPending}>{t.teams.createButton}</button>
      </form>
    </Page>
  )
}
