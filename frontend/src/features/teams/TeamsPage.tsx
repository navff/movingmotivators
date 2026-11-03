import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/api/client'
import { keys } from '@/api/hooks'
import type { Team } from '@/api/types'
import { Page } from '@/components/Layout'
import { Empty, ErrorBox, PageTitle, Spinner, StatusBadge } from '@/components/ui'
import { formatDate, plural, t } from '@/i18n'

export default function TeamsPage() {
  const { data, isLoading, error, refetch } = useQuery({ queryKey: keys.teams, queryFn: () => api.get<Team[]>('/teams') })

  return (
    <Page wide>
      <PageTitle actions={<Link to="/app/teams/new" className="btn-primary">{t.teams.create}</Link>}>{t.teams.title}</PageTitle>
      {isLoading && <Spinner />}
      {error && <ErrorBox error={error} onRetry={refetch} />}
      {data && data.length === 0 && (
        <Empty action={<Link to="/app/teams/new" className="btn-primary">{t.teams.create}</Link>}>{t.teams.empty}</Empty>
      )}
      {data && data.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.map((team) => (
            <Link
              key={team.id}
              to={`/app/teams/${team.id}`}
              className={`panel block transition hover:border-brand hover:shadow-md ${team.archivedAt ? 'opacity-60' : ''}`}
            >
              <div className="mb-1 flex items-start justify-between gap-2">
                <h2 className="text-lg font-bold">{team.name}</h2>
                {team.archivedAt && <span className="text-xs text-zinc-500">{t.teams.archived}</span>}
              </div>
              <p className="mb-3 text-sm text-zinc-500">
                {team.size} {t.teams.people} · {team.surveyCount} {plural(team.surveyCount, ['опрос', 'опроса', 'опросов'])}
                {team.openSurveyCount > 0 && ` · ${team.openSurveyCount} ${t.teams.openSurveys}`}
              </p>
              {team.lastSurvey ? (
                <div className="text-sm">
                  <div className="text-zinc-500">{t.teams.lastSurvey}</div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{team.lastSurvey.title}</span>
                    <StatusBadge status={team.lastSurvey.status} />
                  </div>
                  <div className="text-xs text-zinc-400">{formatDate(team.lastSurvey.createdAt)}</div>
                </div>
              ) : (
                <p className="text-sm text-zinc-400">{t.teams.noSurveys}</p>
              )}
            </Link>
          ))}
        </div>
      )}
    </Page>
  )
}
