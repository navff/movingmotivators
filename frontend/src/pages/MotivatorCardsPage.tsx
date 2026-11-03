import { Link } from 'react-router'
import { MotivatorCard } from '@/components/MotivatorCard'
import { Page } from '@/components/Layout'
import { motivators } from '@/content/motivators'
import { textOn } from '@/lib/colors'
import { ArticleLd, Breadcrumbs, Cta, Faq, ReadNext, type FaqItem } from './seo/Article'

const PATH = '/moving-motivators/kartochki'
const H1 = 'Карточки Moving Motivators: 10 мотиваторов CHAMPFROGS'

const faq: FaqItem[] = [
  {
    q: 'Почему карточек именно десять?',
    a: 'Юрген Аппело выбрал десять мотиваторов как компромисс между полнотой и удобством: их хватает, чтобы описать основные внутренние потребности на работе, и при этом раскладку можно сделать за несколько минут. Первые буквы английских названий складываются в запоминающееся слово CHAMPFROGS.',
  },
  {
    q: 'Чем «Власть» отличается от «Статуса»?',
    a: '«Власть» — про влияние на происходящее: участвовать в решениях, менять процессы. «Статус» — про положение и признание: должность, репутацию, заметность достижений. Человек может хотеть влиять, не стремясь к громкой должности, и наоборот.',
  },
  {
    q: 'Чем «Принятие» отличается от «Взаимосвязи»?',
    a: '«Принятие» — ощущение, что тебя одобряют таким, какой ты есть, и ценят твой вклад. «Взаимосвязь» — наличие хороших отношений с коллегами и чувство принадлежности к команде. Первое про то, как к тебе относятся, второе — про связи между людьми.',
  },
  {
    q: 'Где взять карточки Moving Motivators?',
    a: 'Печатные карточки распространяет Management 3.0 на своём сайте. Если команда распределённая или вам нужна анонимная сводка, проще пройти Moving Motivators онлайн: участники раскладывают те же 10 мотиваторов по ссылке с телефона.',
  },
  {
    q: 'Что делать, если мотиватор внизу списка?',
    a: 'Ничего специального. Карточки в левой части ряда не означают проблему — просто сейчас этот мотиватор слабее влияет на человека. Внимания требуют прежде всего самые важные мотиваторы, особенно если планируемые изменения могут их ослабить.',
  },
]

export default function MotivatorCardsPage() {
  return (
    <Page>
      <article className="space-y-5 leading-relaxed">
        <Breadcrumbs items={[{ path: '/methods/moving-motivators', name: 'Moving Motivators' }, { path: PATH, name: 'Карточки' }]} />
        <ArticleLd path={PATH} headline={H1} />
        <h1 className="text-3xl font-bold">{H1}</h1>
        <p className="text-lg">
          В игре <Link to="/methods/moving-motivators" className="underline">Moving Motivators</Link> десять карточек. Каждая описывает
          внутреннюю потребность, которая делает работу осмысленной. Ниже — что означает каждый мотиватор, по каким признакам его узнать
          и что может сделать руководитель, чтобы его поддержать.
        </p>

        <nav aria-label="Содержание" className="panel">
          <h2 className="mb-2 text-lg font-bold">Содержание</h2>
          <ol className="grid gap-1 sm:grid-cols-2">
            {motivators.map((m) => (
              <li key={m.key}>
                <a href={`#${m.key}`} className="hover:underline"><b>{m.letter}</b> — {m.name} ({m.englishName})</a>
              </li>
            ))}
          </ol>
        </nav>

        {motivators.map((m) => (
          <section key={m.key} id={m.key} className="scroll-mt-20 space-y-3 border-t border-zinc-200 pt-6 dark:border-zinc-800">
            <div className="flex items-start gap-5">
              <MotivatorCard card={m} className="hidden w-40 shrink-0 sm:flex" />
              <div className="min-w-0 space-y-3">
                <h2 className="flex items-center gap-3 text-2xl font-bold">
                  <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full font-slab" style={{ background: m.accentColor, color: textOn(m.accentColor) }}>{m.letter}</span>
                  {m.name} ({m.englishName})
                </h2>
                <p className="italic text-zinc-600 dark:text-zinc-300">«{m.statement}»</p>
                <p>{m.about}</p>
              </div>
            </div>
            <h3 className="text-lg font-bold">Как понять, что мотиватор «{m.name}» важен для человека</h3>
            <p>Обычно такой человек:</p>
            <ul className="list-disc space-y-1 pl-6">{m.signs.map((s) => <li key={s}>{s}</li>)}</ul>
            <h3 className="text-lg font-bold">Как поддержать</h3>
            <ul className="list-disc space-y-1 pl-6">{m.support.map((s) => <li key={s}>{s}</li>)}</ul>
            <p className="rounded-lg bg-zinc-100 p-4 text-sm dark:bg-zinc-900"><b>Если мотиватор не удовлетворён.</b> {m.risk}</p>
          </section>
        ))}

        <section className="space-y-3 border-t border-zinc-200 pt-6 dark:border-zinc-800">
          <h2 className="text-2xl font-bold">Как мотиваторы связаны между собой</h2>
          <p>
            Мотиваторы не независимы. «Свобода», «Мастерство» и «Цель» соответствуют трём основам мотивации из книги Дэниела Пинка «Драйв».
            «Принятие» и «Взаимосвязь» описывают социальную сторону работы, «Порядок» и «Честь» — потребность в ясных и справедливых правилах,
            «Власть» и «Статус» — положение человека в организации, а «Любопытство» — тягу к новому.
          </p>
          <p>
            Иногда мотиваторы конфликтуют: например, сильная потребность в «Порядке» плохо сочетается с хаотичным стартапом, а высокая «Свобода» —
            с жёсткой системой согласований. Такие противоречия и стоит искать при обсуждении раскладки.
          </p>
          <p>
            Как построить разговор по карточкам, смотрите в статье{' '}
            <Link to="/moving-motivators/kak-provesti" className="underline">«Как провести Moving Motivators»</Link>.
          </p>
        </section>

        <Cta title="Узнайте порядок мотиваторов вашей команды" />
        <Faq items={faq} />
        <ReadNext current={PATH} />
      </article>
    </Page>
  )
}
