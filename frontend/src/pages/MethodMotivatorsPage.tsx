import { Link } from 'react-router'
import { useCatalog } from '@/api/hooks'
import { MotivatorCard } from '@/components/MotivatorCard'
import { Page } from '@/components/Layout'
import { t } from '@/i18n'

export default function MethodMotivatorsPage() {
  const { data: catalog } = useCatalog()
  return (
    <Page wide>
      <article className="mx-auto max-w-3xl space-y-5 leading-relaxed">
        <h1 className="text-3xl font-bold">{t.methods.mmTitle}</h1>
        <p>
          «Движущие мотиваторы» (Moving Motivators) — практика Management 3.0 Юргена Аппело. Она опирается на исследования
          внутренней мотивации: людей двигают не только деньги и KPI, но и любопытство, свобода, мастерство, принадлежность к команде.
          У каждого человека эти мотиваторы выстраиваются в свой порядок — и он меняется со временем.
        </p>
        <h2 className="pt-2 text-xl font-bold">Как проходит опрос</h2>
        <ol className="list-decimal space-y-2 pl-6">
          <li><b>Ранжирование.</b> Участник раскладывает 10 карточек от наименее важной к самой важной для себя.</li>
          <li><b>Влияние изменений</b> (если организатор включил). Для каждого предстоящего изменения — реорганизации, переезда,
            нового процесса — участник отмечает, какие мотиваторы станут лучше, а какие пострадают.</li>
          <li><b>Комментарий</b> — что ещё важно знать руководителю.</li>
        </ol>
        <h2 className="pt-2 text-xl font-bold">Что вы получите</h2>
        <ul className="list-disc space-y-1 pl-6">
          <li>Средний ранг каждой карточки, топ-3 и анти-топ-3 команды.</li>
          <li>Тепловую карту распределения — где команда единодушна, а где мнения сильно расходятся.</li>
          <li>Индекс влияния каждого изменения по мотиваторам и общий итог, взвешенный по важности для команды.</li>
          <li>Подсказки и вопросы для разговора с командой.</li>
        </ul>
        <h2 className="pt-2 text-xl font-bold">Как провести сессию по результатам</h2>
        <ol className="list-decimal space-y-2 pl-6">
          <li>Покажите общую картину и топ-3 команды. Спросите: «Что вас удивило?»</li>
          <li>Обсудите карточки, где мнения разошлись, — это не проблема, а повод понять друг друга.</li>
          <li>Если оценивали изменения — начните с того, что угрожает главным мотиваторам команды.</li>
          <li>Договоритесь о 1–2 конкретных шагах и вернитесь к ним через квартал — повторным опросом.</li>
        </ol>
        <p className="rounded-lg bg-amber-50 p-4 text-sm dark:bg-amber-950/40">
          Результаты — повод для разговора, а не оценка сотрудников. Не используйте их как единственное основание для кадровых решений.
        </p>
      </article>

      {catalog && (
        <div className="mt-10 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {catalog.motivators.map((m) => <MotivatorCard key={m.key} card={m} />)}
        </div>
      )}

      <div className="mx-auto mt-10 max-w-3xl space-y-3">
        <h2 className="text-xl font-bold">{t.methods.sources}</h2>
        <ul className="list-disc pl-6 text-sm">
          <li><a className="underline" href="https://management30.com/practice/moving-motivators/" target="_blank" rel="noreferrer">Moving Motivators — Management 3.0</a></li>
          <li>Jurgen Appelo, «Managing for Happiness», 2016.</li>
          <li>Daniel Pink, «Drive: The Surprising Truth About What Motivates Us», 2009.</li>
        </ul>
        <Link to="/login" className="btn-primary mt-4">{t.landing.cta}</Link>
      </div>
    </Page>
  )
}
