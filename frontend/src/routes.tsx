import { lazy, Suspense, type ComponentType } from 'react'
import { Link, type RouteObject } from 'react-router'
import { SiteLayout, RequireAuth, Page } from './components/Layout'
import { Spinner } from './components/ui'
import { t } from './i18n'

/** Каждая страница — отдельный chunk; страница участника особенно лёгкая. */
function page(load: () => Promise<{ default: ComponentType }>) {
  const C = lazy(load)
  return (
    <Suspense fallback={<Spinner />}>
      <C />
    </Suspense>
  )
}

function NotFound() {
  return (
    <Page>
      <h1 className="mb-4 text-2xl font-bold">{t.common.notFound}</h1>
      <Link to="/" className="btn-primary">{t.common.toHome}</Link>
    </Page>
  )
}

/** Браузер создаёт из них browser router (main.tsx), пререндер — memory router в Node (src/prerender.tsx). */
export const routes: RouteObject[] = [
  // прохождение — отдельный лёгкий layout без шапки кабинета
  { path: '/s/:code', element: page(() => import('./features/respond/RespondPage')) },
  {
    element: <SiteLayout />,
    children: [
      { path: '/', element: page(() => import('./pages/LandingPage')) },
      { path: '/methods/moving-motivators', element: page(() => import('./pages/MethodMotivatorsPage')) },
      { path: '/moving-motivators/kartochki', element: page(() => import('./pages/MotivatorCardsPage')) },
      { path: '/moving-motivators/kak-provesti', element: page(() => import('./pages/HowToRunPage')) },
      { path: '/moving-motivators/primenenie', element: page(() => import('./pages/UseCasesPage')) },
      { path: '/methods/health-check', element: page(() => import('./pages/MethodHealthCheckPage')) },
      { path: '/terms', element: page(() => import('./pages/legal/TermsPage')) },
      { path: '/privacy', element: page(() => import('./pages/legal/PrivacyPage')) },
      { path: '/consent', element: page(() => import('./pages/legal/ConsentPage')) },
      { path: '/login', element: page(() => import('./features/auth/LoginPage')) },
      { path: '/auth/verify', element: page(() => import('./features/auth/VerifyPage')) },
      { path: '/account/delete', element: page(() => import('./features/auth/DeleteConfirmPage')) },
      {
        element: <RequireAuth />,
        children: [
          { path: '/onboarding', element: page(() => import('./features/auth/OnboardingPage')) },
          { path: '/app', element: page(() => import('./features/teams/TeamsPage')) },
          { path: '/app/teams/new', element: page(() => import('./features/teams/NewTeamPage')) },
          { path: '/app/teams/:id', element: page(() => import('./features/teams/TeamPage')) },
          { path: '/app/teams/:id/surveys/new', element: page(() => import('./features/surveys/NewSurveyPage')) },
          { path: '/app/surveys/:id', element: page(() => import('./features/results/SurveyPage')) },
          { path: '/app/surveys/:id/share', element: page(() => import('./features/surveys/SharePage')) },
          { path: '/app/surveys/:id/checkout', element: page(() => import('./features/checkout/CheckoutPage')) },
          { path: '/app/settings', element: page(() => import('./features/auth/SettingsPage')) },
        ],
      },
      {
        element: <RequireAuth admin />,
        children: [{ path: '/admin', element: page(() => import('./features/admin/AdminPage')) }],
      },
      { path: '*', element: <NotFound /> },
    ],
  },
]
