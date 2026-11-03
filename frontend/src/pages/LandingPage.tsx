import { Link } from 'react-router'
import { useCatalog, useMe, usePricing } from '@/api/hooks'
import { MotivatorCard } from '@/components/MotivatorCard'
import { motivators } from '@/content/motivators'
import { LoginForm } from '@/features/auth/LoginPage'
import { fmt, t } from '@/i18n'
import { trafficColor } from '@/lib/colors'
import { Faq, JsonLd, type FaqItem } from './seo/Article'
import { SITE_URL } from '@/seo'

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
            <h1 className="mb-5 text-3xl leading-tight font-bold sm:text-5xl">
              <span className="mb-3 block font-slab text-sm tracking-wide text-brand uppercase dark:text-violet-300">{t.landing.heroKicker}</span>
              {t.landing.heroTitle}
            </h1>
            <p className="mb-7 text-lg text-zinc-700 dark:text-zinc-300">{t.landing.heroText}</p>
            {cta}
            <p className="mt-3 text-sm text-zinc-500">{t.landing.ctaNote}</p>
          </div>
          <div className="relative mx-auto h-72 w-full max-w-sm" aria-hidden>
            {['freedom', 'mastery', 'curiosity'].map((k, i) => {
              const card = motivators.find((m) => m.key === k)!
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
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-14">
        <h2 className="mb-2 text-2xl font-bold sm:text-3xl">{t.landing.cardsTitle}</h2>
        <p className="mb-8 max-w-2xl text-zinc-600 dark:text-zinc-300">{t.landing.cardsText}</p>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {motivators.map((m) => <MotivatorCard key={m.key} card={m} showEnglish={catalog?.showEnglishCardNames} />)}
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

      <section className="mx-auto max-w-3xl space-y-4 px-4 py-14 leading-relaxed">
        <h2 className="text-2xl font-bold sm:text-3xl">Что такое Moving Motivators</h2>
        <p>
          <b>Moving Motivators</b> («Движущие мотиваторы») — практика Management 3.0, которую придумал Юрген Аппело. Десять карточек модели
          CHAMPFROGS описывают внутренние мотиваторы: любопытство, честь, принятие, мастерство, власть, свободу, взаимосвязь, порядок, цель и статус.
          Человек раскладывает их по важности для себя, а затем обсуждает расклад с руководителем или командой.
        </p>
        <p>
          Классически Moving Motivators проводят с физическими карточками за одним столом. Наш сервис переносит игру в онлайн:
          участники раскладывают карточки по ссылке с телефона, а руководитель получает готовую сводку команды — средний ранг мотиваторов,
          тепловую карту расхождений, оценку влияния изменений и вопросы для обсуждения. Это удобно для удалённых и гибридных команд
          и когда нужна честная анонимная картина.
        </p>
        <ul className="list-disc space-y-1 pl-6">
          <li><Link to="/methods/moving-motivators" className="underline">Методика Moving Motivators: история, CHAMPFROGS, правила игры</Link></li>
          <li><Link to="/moving-motivators/kartochki" className="underline">Описание 10 карточек мотиваторов</Link></li>
          <li><Link to="/moving-motivators/kak-provesti" className="underline">Как провести Moving Motivators: сценарий и вопросы</Link></li>
          <li><Link to="/moving-motivators/primenenie" className="underline">Где применять: онбординг, 1-на-1, ретро, изменения</Link></li>
        </ul>
        <Faq items={faq(free, price)} />
        <JsonLd
          data={{
            '@type': 'WebApplication',
            name: 'Движущие Мотиваторы — Moving Motivators онлайн',
            url: SITE_URL,
            applicationCategory: 'BusinessApplication',
            operatingSystem: 'Any',
            inLanguage: 'ru',
            offers: { '@type': 'AggregateOffer', priceCurrency: 'RUB', lowPrice: 0, highPrice: price },
          }}
        />
      </section>
    </div>
  )
}

const faq = (free: number, price: number): FaqItem[] => [
  {
    q: 'Сколько стоит провести Moving Motivators онлайн?',
    a: `Опрос для команды до ${free} человек — бесплатно. Для команды больше ${free} человек — ${price} ₽ за опрос, без подписок. Число команд не ограничено.`,
  },
  {
    q: 'Нужна ли участникам регистрация?',
    a: 'Нет. Участники открывают ссылку или QR-код и проходят опрос с телефона или компьютера за 5 минут. Регистрируется только организатор — по email, без пароля.',
  },
  {
    q: 'Это анонимно?',
    a: 'Организатор выбирает режим. В анонимном режиме мы не спрашиваем имя и не сохраняем IP-адрес — видна только общая картина команды. В режиме с именами участник заранее знает, что руководитель увидит его ответы.',
  },
  {
    q: 'Чем онлайн-версия отличается от игры с карточками?',
    a: 'Правила те же — 10 мотиваторов CHAMPFROGS и оценка влияния изменений. Онлайн добавляет автоматическую сводку по команде, тепловую карту, анонимность, историю опросов и сравнение во времени.',
  },
  {
    q: 'Moving Motivators — это психологический тест?',
    a: 'Нет. Это инструмент для разговора, а не диагностика личности: правильного порядка карточек не существует. Результаты не стоит использовать как основание для кадровых решений.',
  },
]
