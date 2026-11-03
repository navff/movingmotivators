import { Link, NavLink, Navigate, Outlet, useLocation, useNavigate } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { t } from '@/i18n'
import { useMe, useSite } from '@/api/hooks'
import { api } from '@/api/client'
import { Spinner } from './ui'

function Logo() {
  return (
    <Link to="/" className="flex items-center gap-2 font-slab text-base font-bold text-brand dark:text-violet-300">
      <img src="/favicon.svg" alt="" className="h-7 w-7" />
      <span className="hidden sm:inline">{t.common.productName}</span>
      <span className="sm:hidden">Мотиваторы</span>
    </Link>
  )
}

function Header() {
  const { data: me } = useMe()
  const qc = useQueryClient()
  const navigate = useNavigate()
  const linkCls = ({ isActive }: { isActive: boolean }) =>
    `rounded-md px-2.5 py-1.5 text-sm ${isActive ? 'font-semibold text-brand dark:text-violet-300' : 'text-zinc-600 hover:text-zinc-900 dark:text-zinc-400 dark:hover:text-zinc-100'}`

  return (
    <header className="sticky top-0 z-20 border-b border-zinc-200 bg-white/90 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/90">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between gap-2 px-4">
        <Logo />
        <nav className="flex items-center gap-1">
          {me ? (
            <>
              <NavLink to="/app" end className={linkCls}>{t.nav.app}</NavLink>
              {me.isAdmin && <NavLink to="/admin" className={linkCls}>{t.nav.admin}</NavLink>}
              <NavLink to="/app/settings" className={linkCls}>{t.nav.settings}</NavLink>
              <button
                className="rounded-md px-2.5 py-1.5 text-sm text-zinc-600 hover:text-zinc-900 dark:text-zinc-400"
                onClick={async () => {
                  await api.post('/auth/logout')
                  qc.setQueryData(['me'], null)
                  qc.removeQueries({ queryKey: ['teams'] })
                  navigate('/')
                }}
              >
                {t.nav.logout}
              </button>
            </>
          ) : (
            <>
              <NavLink to="/methods/moving-motivators" className={(p) => `${linkCls(p)} hidden sm:inline`}>{t.nav.methods}</NavLink>
              <Link to="/login" className="btn-primary min-h-9 px-4 py-1.5 text-sm">{t.nav.login}</Link>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}

export function Footer() {
  const { data: site } = useSite()
  const legal = site?.legal
  return (
    <footer className="mt-16 border-t border-zinc-200 py-8 text-sm text-zinc-500 dark:border-zinc-800 dark:text-zinc-400">
      <div className="mx-auto max-w-6xl space-y-2 px-4">
        <p>
          <a href="https://management30.com/practice/moving-motivators/" target="_blank" rel="noreferrer" className="underline-offset-2 hover:underline">
            {t.footer.mmCopyright}
          </a>
          {' · '}
          {t.footer.hcCredit}
        </p>
        <p>
          {legal ? `${legal.shortName}, ИНН ${legal.inn}, ОГРНИП ${legal.ogrnip}` : 'ИП Варнавский В. Н., ИНН 352826429326, ОГРНИП 308352816800157'}
          {' · '}
          <a href={`mailto:${legal?.email ?? 'support@movingmotivators.ru'}`} className="hover:underline">
            {legal?.email ?? 'support@movingmotivators.ru'}
          </a>
          {' · '}
          <Link to="/terms" className="hover:underline">{t.footer.terms}</Link>
          {' · '}
          <Link to="/privacy" className="hover:underline">{t.footer.privacy}</Link>
          {' · '}
          <Link to="/consent" className="hover:underline">{t.footer.consent}</Link>
        </p>
      </div>
    </footer>
  )
}

export function SiteLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <Header />
      <main className="flex-1">
        <Outlet />
      </main>
      <Footer />
    </div>
  )
}

/** Контейнер страниц кабинета. */
export function Page({ children, wide }: { children: React.ReactNode; wide?: boolean }) {
  return <div className={`mx-auto px-4 py-8 ${wide ? 'max-w-6xl' : 'max-w-3xl'}`}>{children}</div>
}

/** Только для вошедших; без согласия — на онбординг. */
export function RequireAuth({ admin }: { admin?: boolean }) {
  const { data: me, isLoading } = useMe()
  const location = useLocation()
  if (isLoading) return <Spinner />
  if (!me) {
    const returnUrl = location.pathname + location.search
    return <Navigate to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`} replace />
  }
  if (me.needsOnboarding && location.pathname !== '/onboarding')
    return <Navigate to={`/onboarding?returnUrl=${encodeURIComponent(location.pathname + location.search)}`} replace />
  if (admin && !me.isAdmin) return <Navigate to="/app" replace />
  return <Outlet />
}
