import { useEffect, useRef, useState, type ReactNode } from 'react'
import { t } from '@/i18n'
import { errorMessage } from '@/api/client'

export function Spinner({ label = t.common.loading }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-16 text-zinc-500" role="status">
      <span className="h-5 w-5 animate-spin rounded-full border-2 border-zinc-300 border-t-brand" aria-hidden />
      {label}
    </div>
  )
}

export function ErrorBox({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200" role="alert">
      {errorMessage(error)}
      {onRetry && (
        <button className="ml-3 underline" onClick={onRetry}>
          Повторить
        </button>
      )}
    </div>
  )
}

export function Empty({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="rounded-xl border-2 border-dashed border-zinc-300 p-8 text-center text-zinc-600 dark:border-zinc-700 dark:text-zinc-400">
      <p className="mx-auto max-w-md">{children}</p>
      {action && <div className="mt-5">{action}</div>}
    </div>
  )
}

export function Tabs<T extends string>({
  tabs,
  value,
  onChange,
}: {
  tabs: { id: T; label: string }[]
  value: T
  onChange: (v: T) => void
}) {
  return (
    <div className="-mx-4 mb-6 overflow-x-auto px-4 sm:mx-0 sm:px-0" role="tablist">
      <div className="flex min-w-max gap-1 border-b border-zinc-200 dark:border-zinc-800">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            role="tab"
            aria-selected={value === tab.id}
            onClick={() => onChange(tab.id)}
            className={`-mb-px border-b-2 px-4 py-2.5 text-sm font-medium transition ${
              value === tab.id
                ? 'border-brand text-brand dark:border-violet-400 dark:text-violet-300'
                : 'border-transparent text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>
    </div>
  )
}

/** Модальное окно на <dialog> — без браузерных confirm(). */
export function Modal({ open, onClose, title, children }: { open: boolean; onClose: () => void; title: string; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const d = ref.current
    if (!d) return
    if (open && !d.open) d.showModal()
    if (!open && d.open) d.close()
  }, [open])
  return (
    <dialog
      ref={ref}
      onClose={onClose}
      onClick={(e) => e.target === ref.current && onClose()}
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-xl bg-white p-0 text-zinc-900 shadow-xl backdrop:bg-black/40 dark:bg-zinc-900 dark:text-zinc-100"
    >
      {open && (
        <div className="p-6">
          <h2 className="mb-4 text-lg font-bold">{title}</h2>
          {children}
        </div>
      )}
    </dialog>
  )
}

export function ConfirmModal({
  open, title, text, confirmLabel, danger, onConfirm, onClose, busy,
}: {
  open: boolean
  title: string
  text: ReactNode
  confirmLabel: string
  danger?: boolean
  busy?: boolean
  onConfirm: () => void
  onClose: () => void
}) {
  return (
    <Modal open={open} onClose={onClose} title={title}>
      <div className="mb-6 text-zinc-600 dark:text-zinc-300">{text}</div>
      <div className="flex flex-wrap justify-end gap-2">
        <button className="btn-secondary" onClick={onClose}>{t.common.cancel}</button>
        <button className={danger ? 'btn-danger' : 'btn-primary'} onClick={onConfirm} disabled={busy}>{confirmLabel}</button>
      </div>
    </Modal>
  )
}

export function CopyButton({ text, label = t.common.copy, className = 'btn-secondary' }: { text: string; label?: string; className?: string }) {
  const [done, setDone] = useState(false)
  return (
    <button
      type="button"
      className={className}
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(text)
        } catch {
          const ta = document.createElement('textarea')
          ta.value = text
          document.body.appendChild(ta)
          ta.select()
          document.execCommand('copy')
          ta.remove()
        }
        setDone(true)
        setTimeout(() => setDone(false), 2000)
      }}
    >
      {done ? `✓ ${t.common.copied}` : label}
    </button>
  )
}

export function StatusBadge({ status }: { status: string }) {
  const cls =
    status === 'open'
      ? 'bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300'
      : status === 'draft'
        ? 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300'
        : 'bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300'
  return <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${cls}`}>{t.status[status] ?? status}</span>
}

export function PageTitle({ children, actions }: { children: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
      <h1 className="text-2xl font-bold sm:text-3xl">{children}</h1>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}
