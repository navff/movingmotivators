import { Link } from 'react-router'
import { useCatalog, useMe, usePricing } from '@/api/hooks'
import { MotivatorCard } from '@/components/MotivatorCard'
import { LoginForm } from '@/features/auth/LoginPage'
import { fmt, t } from '@/i18n'
import { trafficColor } from '@/lib/colors'

export default function LandingPage() {
  const { data: catalog } = useCatalog()
  const { data: pricing } = usePricing()
  const { data: me } = useMe()
  const free = pricing?.freeMaxParticipants ?? 8
  const price = pricing?.surveyPriceRub ?? 299
  const cta = me ? (
    <Link to="/app" className="btn-primary">{t.nav.app} →</Link>
  ) : (
    <div className="max-w-md"><LoginForm compact /></div>
  )

  return (
    <div>
      <section className="bg-gradient-to-b from-brand-50 to-transparent dark:from-zinc-900">
        <div className="mx-auto grid max-w-6xl items-center gap-10 px-4 py-14 md:grid-cols-[1.1fr_1fr] md:py-20">
          <div>
            <h1 className="mb-5 text-3xl leading-tight font-bold sm:text-5xl">{t.landing.heroTitle}</h1>
            <p className="mb-7 text-lg text-zinc-700 dark:text-zinc-300">{t.landing.heroText}</p>
            {cta}
            <p className="mt-3 text-sm text-zinc-500">{t.landing.ctaNote}</p>
          </div>
          {catalog && (
            <div className="relative mx-auto h-72 w-full max-w-sm" aria-hidden>
              {['freedom', 'mastery', 'curiosity'].map((k, i) => {
                const card = catalog.motivators.find((m) => m.key === k)!
                return (
                  <MotivatorCard
                    key={k}
                    card={card}
                    className="absolute top-0 w-44"
                    style={{ left: `${i * 26}%`, transform: `rotate(${(i - 1) * 8}deg) translateY(${i === 1 ? -10 : 10}px)`, zIndex: i === 1 ? 2 : 1 }}
                  />
                )
              })}
            </div>
          )}
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-14">
        <h2 className="mb-2 text-2xl font-bold sm:text-3xl">{t.landing.cardsTitle}</h2>
        <p className="mb-8 max-w-2xl text-zinc-600 dark:text-zinc-300">{t.landing.cardsText}</p>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {catalog?.motivators.map((m) => <MotivatorCard key={m.key} card={m} showEnglish={catalog.showEnglishCardNames} />)}
        </div>
        <Link to="/methods/moving-motivators" className="btn-ghost mt-6 px-0">{t.landing.learnMore} →</Link>
      </section>

      <section className="bg-white py-14 dark:bg-zinc-900">
        <div className="mx-auto max-w-6xl px-4">
          <h2 className="mb-8 text-2xl font-bold sm:text-3xl">{t.landing.howTitle}</h2>
          <ol className="grid gap-6 md:grid-cols-3">
            {t.landing.how.map((s, i) => (
              <li key={s.title} className="panel">
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-brand font-slab text-lg font-bold text-white">{i + 1}</div>
                <h3 className="mb-1 text-lg font-bold">{s.title}</h3>
                <p className="text-zinc-600 dark:text-zinc-300">{s.text}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="mx-auto grid max-w-6xl gap-6 px-4 py-14 md:grid-cols-3">
        <div className="panel">
          <h2 className="mb-2 text-xl font-bold">{t.landing.impactTitle}</h2>
          <p className="text-zinc-600 dark:text-zinc-300">{t.landing.impactText}</p>
        </div>
        <div className="panel">
          <h2 className="mb-2 text-xl font-bold">{t.landing.hcTitle}</h2>
          <p className="mb-3 text-zinc-600 dark:text-zinc-300">{t.landing.hcText}</p>
          <div className="flex gap-2" aria-hidden>
            {(['green', 'yellow', 'red'] as const).map((c) => <span key={c} className="h-6 w-6 rounded-full" style={{ background: trafficColor(c) }} />)}
          </div>
          <Link to="/methods/health-check" className="btn-ghost mt-2 px-0">{t.landing.learnMore} →</Link>
        </div>
        <div className="panel">
          <h2 className="mb-2 text-xl font-bold">{t.landing.anonTitle}</h2>
          <p className="text-zinc-600 dark:text-zinc-300">{t.landing.anonText}</p>
        </div>
      </section>

      <section id="pricing" className="bg-white py-14 dark:bg-zinc-900">
        <div className="mx-auto max-w-3xl px-4 text-center">
          <h2 className="mb-8 text-2xl font-bold sm:text-3xl">{t.landing.priceTitle}</h2>
          <div className="mb-6 grid gap-4 sm:grid-cols-2">
            <div className="panel">
              <div className="text-zinc-500">{fmt(t.landing.priceFree, { n: free })}</div>
              <div className="mt-1 font-slab text-3xl font-bold text-green-700 dark:text-green-400">{t.landing.priceFreeValue}</div>
            </div>
            <div className="panel">
              <div className="text-zinc-500">{fmt(t.landing.pricePaid, { n: free })}</div>
              <div className="mt-1 font-slab text-3xl font-bold">{fmt(t.landing.pricePaidValue, { price })}</div>
            </div>
          </div>
          <p className="mb-8 text-zinc-600 dark:text-zinc-300">{t.landing.priceNote}</p>
          <div className="flex justify-center">{cta}</div>
        </div>
      </section>
    </div>
  )
}
