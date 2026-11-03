import { useEffect } from 'react'
import { useRouteError } from 'react-router'
import { t } from '@/i18n'
import { isChunkLoadError, reloadOnce } from '@/lib/chunkReload'

/**
 * errorElement для корневых маршрутов: вместо отладочного экрана React Router.
 * Не зависит от SiteLayout и данных API — ошибка могла случиться именно там.
 */
export function RouteError() {
  const error = useRouteError()
  const stale = isChunkLoadError(error)

  // устаревшая вкладка после деплоя: пробуем перезагрузиться сами, кнопка — если не вышло
  useEffect(() => {
    if (stale) reloadOnce()
    else console.error(error)
  }, [stale, error])

  return (
    <div className="flex min-h-dvh items-center justify-center bg-zinc-50 px-4 dark:bg-zinc-950">
      <div className="panel max-w-md text-center">
        <img src="/favicon.svg" alt="" className="mx-auto mb-4 h-10 w-10" />
        <h1 className="mb-2 text-2xl font-bold">{stale ? t.common.updatedTitle : t.common.errorTitle}</h1>
        <p className="mb-6 text-zinc-600 dark:text-zinc-300">{stale ? t.common.updatedText : t.common.errorText}</p>
        <div className="flex flex-wrap justify-center gap-2">
          <button className="btn-primary" onClick={() => window.location.reload()}>{t.common.reload}</button>
          <a href="/" className="btn-secondary">{t.common.toHome}</a>
        </div>
      </div>
    </div>
  )
}
