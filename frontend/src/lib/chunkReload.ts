/**
 * После деплоя у файлов сборки новые хеши, а старые удаляются. Вкладка, открытая до деплоя,
 * при переходе по ссылке запрашивает чанк по старому имени и получает 404
 * («Failed to fetch dynamically imported module»). Лечится перезагрузкой: свежий index.html
 * ссылается на новые файлы.
 */
const KEY = 'chunk-reload-at'
/** Не перезагружаем чаще: если и после перезагрузки чанка нет — это уже не устаревшая вкладка. */
const MIN_INTERVAL_MS = 10_000

export function isChunkLoadError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '')
  return /Failed to fetch dynamically imported module|error loading dynamically imported module|Importing a module script failed|Unable to preload CSS|Loading (CSS )?chunk .* failed/i.test(message)
}

/** Перезагружает страницу, если в последние 10 секунд этого не делали. true — перезагрузка началась. */
export function reloadOnce(): boolean {
  if (typeof window === 'undefined') return false
  try {
    const last = Number(sessionStorage.getItem(KEY) ?? 0)
    if (Date.now() - last < MIN_INTERVAL_MS) return false
    sessionStorage.setItem(KEY, String(Date.now()))
  } catch {
    // sessionStorage недоступен — перезагружаем без защиты от цикла: хуже не станет, страница всё равно сломана
  }
  window.location.reload()
  return true
}

/** Обёртка для import() страниц: при устаревшем чанке — тихая перезагрузка вместо ошибки. */
export function withChunkReload<T>(load: () => Promise<T>): () => Promise<T> {
  return () =>
    load().catch((error: unknown) => {
      // промис не завершается: страница вот-вот перезагрузится, показываем спиннер, а не ошибку
      if (isChunkLoadError(error) && reloadOnce()) return new Promise<T>(() => {})
      throw error
    })
}
