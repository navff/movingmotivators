import { Link } from 'react-router'
import { useCatalog } from '@/api/hooks'
import { Page } from '@/components/Layout'
import { t } from '@/i18n'

export default function MethodHealthCheckPage() {
  const { data: catalog } = useCatalog()
  return (
    <Page>
      <article className="space-y-5 leading-relaxed">
        <h1 className="text-3xl font-bold">{t.methods.hcTitle}</h1>
        <p>
          Health Check — регулярная самодиагностика команды по модели Squad Health Check, которую придумали и описали в Spotify.
          Команда оценивает 10 сторон своей работы «светофором» и отмечает, куда всё движется. Ценность — не в разовом снимке,
          а в истории: проводите опрос волнами раз в квартал и смотрите на тренд.
        </p>
        <h2 className="pt-2 text-xl font-bold">Как проходит опрос</h2>
        <p>По каждой категории участник выбирает 🟢 Отлично, 🟡 Есть проблемы или 🔴 Плохо, отмечает тренд (↑ улучшается, → без изменений, ↓ ухудшается) и при желании оставляет комментарий.</p>

        {catalog && (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="text-left">
                <tr><th className="py-2 pr-4">Категория</th><th className="pr-4">🟢 Отлично</th><th>🔴 Плохо</th></tr>
              </thead>
              <tbody>
                {catalog.healthCheckCategories.map((c) => (
                  <tr key={c.key} className="border-t border-zinc-200 dark:border-zinc-800">
                    <td className="py-2 pr-4 align-top font-medium">{c.name}</td>
                    <td className="py-2 pr-4 align-top">{c.awesome}</td>
                    <td className="py-2 align-top">{c.awful}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <h2 className="pt-2 text-xl font-bold">Как провести сессию по результатам</h2>
        <ol className="list-decimal space-y-2 pl-6">
          <li>Начните с блока «Обсудите в первую очередь» — категорий с низким баллом или ухудшением.</li>
          <li>Сравните с прошлой волной: что стало лучше и почему?</li>
          <li>Выберите 1–2 категории и договоритесь о конкретных шагах до следующей волны.</li>
        </ol>
        <h2 className="pt-2 text-xl font-bold">{t.methods.sources}</h2>
        <ul className="list-disc pl-6 text-sm">
          <li><a className="underline" href="https://engineering.atspotify.com/2014/09/squad-health-check-model/" target="_blank" rel="noreferrer">Squad Health Check model — Spotify Engineering</a></li>
        </ul>
        <Link to="/login" className="btn-primary">{t.landing.cta}</Link>
      </article>
    </Page>
  )
}
