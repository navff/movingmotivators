import { StrictMode } from 'react'
import { prerenderToNodeStream } from 'react-dom/static'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from './routes'

export { seoPages, SITE_URL } from './seo'

/**
 * HTML страницы для поисковиков. Запросы к API на сервере не выполняются,
 * поэтому всё, что должно попасть в индекс, страницы берут из статического контента.
 */
export async function render(url: string): Promise<string> {
  const router = createMemoryRouter(routes, { initialEntries: [url] })
  const { prelude } = await prerenderToNodeStream(
    <StrictMode>
      <QueryClientProvider client={new QueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
    // иначе крупные Suspense-блоки выносятся в скрытые сегменты и вставляются inline-скриптом:
    // его блокирует CSP, а без JS (и для поисковика) текст остаётся скрытым
    { progressiveChunkSize: Infinity },
  )
  let html = ''
  for await (const chunk of prelude) html += chunk
  return html
}
