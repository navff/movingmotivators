import Markdown from 'react-markdown'
import { Link } from 'react-router'
import type { ReactNode } from 'react'

function text(children: ReactNode): string {
  if (typeof children === 'string') return children
  if (Array.isArray(children)) return children.map(text).join('')
  return ''
}

/** Markdown юр. документов: внутренние ссылки — через роутер, якоря у заголовков по префиксу текста. */
export function Prose({ source, anchors = {} }: { source: string; anchors?: Record<string, string> }) {
  const idFor = (children: ReactNode) => {
    const s = text(children)
    return Object.entries(anchors).find(([prefix]) => s.includes(prefix))?.[1]
  }
  return (
    <div className="prose-legal space-y-4 leading-relaxed text-zinc-800 dark:text-zinc-200">
      <Markdown
        components={{
          h1: ({ children }) => <h1 className="mb-2 text-2xl font-bold sm:text-3xl">{children}</h1>,
          h2: ({ children }) => <h2 id={idFor(children)} className="scroll-mt-20 pt-4 text-xl font-bold">{children}</h2>,
          h3: ({ children }) => <h3 className="pt-2 text-lg font-bold">{children}</h3>,
          blockquote: ({ children }) => (
            <blockquote className="rounded-lg border-l-4 border-brand bg-brand-50 px-4 py-2 dark:bg-zinc-900">{children}</blockquote>
          ),
          ul: ({ children }) => <ul className="list-disc space-y-1 pl-6">{children}</ul>,
          ol: ({ children }) => <ol className="list-decimal space-y-1 pl-6">{children}</ol>,
          table: ({ children }) => <div className="overflow-x-auto"><table className="min-w-full text-sm">{children}</table></div>,
          td: ({ children }) => <td className="border-t border-zinc-200 py-1.5 pr-4 align-top dark:border-zinc-800">{children}</td>,
          th: ({ children }) => <th className="py-1.5 pr-4 text-left">{children}</th>,
          a: ({ href = '', children }) =>
            href.startsWith('/') ? (
              <Link to={href} className="text-brand underline dark:text-violet-300">{children}</Link>
            ) : (
              <a href={href} target="_blank" rel="noreferrer" className="text-brand underline dark:text-violet-300">{children}</a>
            ),
        }}
      >
        {source}
      </Markdown>
    </div>
  )
}
