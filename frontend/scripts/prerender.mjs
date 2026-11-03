// Пререндер публичных страниц после `vite build`: dist/<путь>/index.html + sitemap.xml.
// Пустая оболочка SPA сохраняется в dist/spa.html — её nginx отдаёт для остальных маршрутов.
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const dist = join(root, 'dist')
const ssrDir = join(root, 'dist-ssr')

const { render, seoPages, SITE_URL } = await import(pathToFileURL(join(ssrDir, 'prerender.js')).href)

const escape = (s) => s.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

const template = readFileSync(join(dist, 'index.html'), 'utf8')
writeFileSync(join(dist, 'spa.html'), template)

for (const [path, meta] of Object.entries(seoPages)) {
  const url = SITE_URL + path
  const head = [
    `<title>${escape(meta.title)}</title>`,
    `<meta name="description" content="${escape(meta.description)}" />`,
    `<link rel="canonical" href="${url}" />`,
    `<meta property="og:title" content="${escape(meta.title)}" />`,
    `<meta property="og:description" content="${escape(meta.description)}" />`,
    `<meta property="og:url" content="${url}" />`,
  ].join('\n    ')
  const body = await render(path)
  const html = template
    .replace(/<title>.*?<\/title>/s, '')
    .replace(/<meta name="description"[^>]*>/, head)
    .replace('<div id="root"></div>', `<div id="root">${body}</div>`)
  if (html === template || !body) throw new Error(`prerender failed: ${path}`)

  const out = join(dist, path, 'index.html')
  mkdirSync(dirname(out), { recursive: true })
  writeFileSync(out, html)
  console.log(`prerendered ${path} (${Math.round(body.length / 1024)} KB)`)
}

const today = new Date().toISOString().slice(0, 10)
const sitemap = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${Object.entries(seoPages)
  .map(([path, m]) => `  <url><loc>${SITE_URL}${path}</loc><lastmod>${today}</lastmod><priority>${m.priority}</priority></url>`)
  .join('\n')}
</urlset>
`
writeFileSync(join(dist, 'sitemap.xml'), sitemap)
rmSync(ssrDir, { recursive: true, force: true })
