import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import QRCode from 'qrcode'
import { api } from '@/api/client'
import { keys } from '@/api/hooks'
import type { Survey } from '@/api/types'
import { Page } from '@/components/Layout'
import { CopyButton, ErrorBox, Spinner } from '@/components/ui'
import { fmt, formatDateTime, t } from '@/i18n'

/** Ссылка строится от текущего origin, чтобы работать и локально, и в проде. */
export const participantUrl = (s: Survey) => `${window.location.origin}/s/${s.publicCode}`

export function inviteText(s: Survey) {
  return fmt(t.share.inviteText, {
    type: t.types[s.type],
    minutes: s.type === 'moving_motivators' ? 7 : 5,
    anon: s.isAnonymous ? t.share.inviteAnon : t.share.inviteNamed,
    url: participantUrl(s),
    date: formatDateTime(s.closesAt),
  })
}

export function ShareBlock({ survey }: { survey: Survey }) {
  const url = participantUrl(survey)
  const [qr, setQr] = useState<string>()
  useEffect(() => {
    QRCode.toDataURL(url, { width: 560, margin: 1, color: { dark: '#1a1a1a', light: '#ffffff' } }).then(setQr)
  }, [url])
  const invite = inviteText(survey)

  return (
    <div className="space-y-6">
      <section className="panel">
        <div className="label">{t.share.link}</div>
        <div className="flex flex-col gap-2 sm:flex-row">
          <input className="input font-mono text-sm" readOnly value={url} onFocus={(e) => e.target.select()} aria-label={t.share.link} />
          <CopyButton text={url} className="btn-primary" />
        </div>
      </section>
      <div className="grid gap-6 md:grid-cols-[auto_1fr]">
        <section className="panel text-center">
          <div className="label">{t.share.qr}</div>
          {qr ? <img src={qr} alt={url} className="mx-auto h-56 w-56 rounded bg-white p-2" /> : <div className="h-56 w-56" />}
          <p className="hint mx-auto max-w-56">{t.share.qrHint}</p>
          {qr && (
            <a href={qr} download={`qr-${survey.publicCode}.png`} className="btn-ghost mt-2 text-sm">PNG</a>
          )}
        </section>
        <section className="panel">
          <div className="label">{t.share.invite}</div>
          <textarea className="input mb-3 min-h-40 text-sm" readOnly value={invite} aria-label={t.share.invite} />
          <CopyButton text={invite} label={t.share.copyInvite} />
        </section>
      </div>
    </div>
  )
}

export default function SharePage() {
  const { id = '' } = useParams()
  const [params] = useSearchParams()
  const launchedFree = params.get('launched') === 'free'
  const { data: survey, error } = useQuery({ queryKey: keys.survey(id), queryFn: () => api.get<Survey>(`/surveys/${id}`) })

  if (error) return <Page><ErrorBox error={error} /></Page>
  if (!survey) return <Spinner />

  return (
    <Page>
      {launchedFree ? (
        <div className="mb-6 rounded-xl border border-green-200 bg-green-50 p-5 dark:border-green-900 dark:bg-green-950">
          <h1 className="mb-2 text-2xl font-bold">{t.share.launchedFree}</h1>
          <p className="text-zinc-700 dark:text-zinc-300">{t.share.earlyAccess}</p>
        </div>
      ) : (
        <h1 className="mb-2 text-3xl font-bold">{t.share.launched}</h1>
      )}
      <p className="mb-6 text-zinc-600 dark:text-zinc-400">
        «{survey.title}» · {survey.teamName}
      </p>
      <ShareBlock survey={survey} />
      <div className="mt-6">
        <Link to={`/app/surveys/${id}`} className="btn-secondary">{t.share.toResults} →</Link>
      </div>
    </Page>
  )
}
