# Деплой в Coolify

Инструкция по первому развёртыванию «Движущих Мотиваторов» на VPS Timeweb Cloud с Coolify v4 и по дальнейшим обновлениям.

## Что получится

```
Интернет ──HTTPS──▶ Traefik (Coolify, Let's Encrypt)
                       │
                       ▼
                 web (nginx :80) ── статика SPA
                       │ /api, /health
                       ▼
                 api (.NET :8080) ──▶ PostgreSQL 17 (Database-ресурс Coolify)
                       │                  └─ ежедневный бэкап → Timeweb Cloud S3
                       └──SMTP──▶ почтовый провайдер (РФ)
```

- Приложение — один ресурс **Docker Compose** из файла [docker-compose.yml](../docker-compose.yml) с двумя сервисами: `web` и `api`.
- База — отдельный ресурс **PostgreSQL 17**, не внутри compose: так в Coolify работают бэкапы по расписанию и данные переживают пересоздание приложения.
- Домен назначается только сервису `web`. `api` наружу не публикуется, nginx проксирует `/api` внутри сети.

## 0. Что понадобится

| Что | Где взять |
|---|---|
| VPS в российском ДЦ, Ubuntu 22.04/24.04, от 2 vCPU / 4 ГБ RAM / 40 ГБ | Timeweb Cloud |
| Установленный Coolify v4 | `curl -fsSL https://cdn.coollabs.io/coolify/install.sh \| sudo bash`, затем панель на `http://<IP>:8000` |
| Доступ к DNS домена `movingmotivators.ru` | регистратор или DNS Timeweb |
| S3-бакет для бэкапов | Timeweb Cloud → Хранилище S3 |
| SMTP-ящик `no-reply@movingmotivators.ru` | Unisender Go, Яндекс 360 или другой российский провайдер |
| Репозиторий на GitHub | `https://github.com/navff/movingmotivators` |

Сборка образов идёт на сервере. .NET SDK и `npm ci` требуют памяти, поэтому на 2 ГБ RAM сборка может падать. Если сервер маленький, добавьте swap на 2–4 ГБ.

## 1. DNS

Создайте две A-записи на IP сервера:

| Имя | Тип | Значение |
|---|---|---|
| `movingmotivators.ru` (`@`) | A | `<IP VPS>` |
| `www` | A | `<IP VPS>` |

Дождитесь, пока записи начнут резолвиться: `nslookup movingmotivators.ru`. Без этого Let's Encrypt не выпустит сертификат.

## 2. S3 для бэкапов

1. В Timeweb Cloud создайте бакет, например `motivators-backups`, и ключи доступа.
2. В Coolify откройте **Storages → S3 Storages → + Add** и заполните:
   - Endpoint — из панели Timeweb (например, `https://s3.timeweb.cloud`);
   - Region — регион бакета (например, `ru-1`);
   - Bucket, Access Key, Secret Key.
3. Нажмите **Validate Connection**.

## 3. База данных PostgreSQL

1. В проекте Coolify нажмите **+ New → Database → PostgreSQL** и выберите образ `postgres:17-alpine`.
2. Задайте:
   - **Database** — `motivators`;
   - **User** — `motivators`;
   - **Password** — сгенерированный.
3. Порт наружу **не публикуйте** (галочку *Make it publicly available* не ставьте). База будет доступна только из docker-сети Coolify.
4. Нажмите **Start**.
5. Скопируйте внутренний хост: Coolify показывает **Postgres URL (internal)** вида `postgres://motivators:<pass>@<uuid>:5432/motivators`. Хост — это `<uuid>`.
6. На вкладке **Backups** добавьте расписание:
   - Frequency — `0 3 * * *` (каждый день в 03:00);
   - Save to S3 — хранилище из шага 2;
   - хранение — **14 дней** (поле *Number of backups to keep* или *Retention*, в зависимости от версии Coolify).

   Сразу нажмите **Backup now** и убедитесь, что файл появился в бакете.

Приложение использует формат строки подключения Npgsql, а не URI. Соберите строку из данных шага 5:

```
Host=<uuid>;Port=5432;Database=motivators;Username=motivators;Password=<pass>
```

## 4. Приложение (Docker Compose)

1. Нажмите **+ New → Application → Public/Private Repository**. Для приватного репозитория сначала подключите **GitHub App** в *Sources*.
2. Укажите:
   - **Repository** — `navff/movingmotivators`;
   - **Branch** — ветка, из которой деплоите;
   - **Build Pack** — **Docker Compose**;
   - **Docker Compose Location** — `/docker-compose.yml`, **Base Directory** — `/`.
3. Нажмите **Continue**. Coolify прочитает compose и покажет сервисы `api` и `web`.

### 4.1. Сеть

В настройках ресурса включите **Connect To Predefined Network**. Без этого контейнер `api` не увидит базу из шага 3: у compose-ресурса своя сеть, а у базы — общая сеть Coolify.

### 4.2. Домен

У сервиса **web** в поле **Domains** укажите:

```
https://movingmotivators.ru,https://www.movingmotivators.ru
```

Порт добавлять не нужно: nginx слушает 80. Запросы на `www` nginx сам перенаправляет на корневой домен с кодом 301.

У сервиса **api** поле домена оставьте **пустым**.

### 4.3. Переменные окружения

На вкладке **Environment Variables** задайте переменные из [.env.example](../.env.example). Галочку **Build Variable** (*Available at Buildtime*) ставить не нужно: образы собираются без секретов, переменные нужны только при запуске. Если `DB_CONNECTION_STRING` или `SMTP_HOST` не заданы, сборка пройдёт, но `api` не запустится, а в логе будет понятная ошибка с именем переменной.

Обязательные переменные:

| Переменная | Значение |
|---|---|
| `DB_CONNECTION_STRING` | строка из шага 3 |
| `SMTP_HOST` | например, `smtp.yandex.ru` |
| `SMTP_USERNAME`, `SMTP_PASSWORD` | логин и пароль приложения почтового ящика |
| `SMTP_FROM` | адрес отправителя; у Яндекса должен совпадать с `SMTP_USERNAME`, иначе письма отклоняются. По умолчанию `no-reply@movingmotivators.ru` |
| `ADMIN_EMAILS` | ваш email для доступа к `/admin`; несколько — через запятую |

Наблюдаемость в Monium, по желанию (подробнее в разделе [Monium](#monium-логи-и-трейсы)):

| Переменная | Значение |
|---|---|
| `MONIUM__ENABLED` | `true` |
| `MONIUM__APIKEY` | API-ключ сервисного аккаунта |
| `MONIUM__PROJECT` | ID проекта Monium |
| `MONIUM__CLUSTER` | `production` |

Пароли и ключи отметьте как **секреты** (*Is Secret / Lock*), чтобы они не показывались в логах сборки.

Остальные настройки лежат в [appsettings.json](../backend/src/Motivators.Api/appsettings.json): реквизиты ИП, цены, адрес сайта `https://movingmotivators.ru`, отправитель писем, SMTP-порт 465 с SSL. Чтобы поменять их, правьте файл и делайте push. Если какую-то настройку нужно задавать именно из Coolify, добавьте её строкой в `environment` сервиса `api` в [docker-compose.yml](../docker-compose.yml), например `Smtp__Port: "${SMTP_PORT:-465}"`. В режиме Docker Compose Coolify передаёт в контейнер только те переменные, которые упомянуты в этом файле.

### 4.4. Первый деплой

1. Нажмите **Deploy**.
2. В логе сборки соберутся оба образа. При старте `api` сам применит миграции БД (`Db__MigrateOnStartup=true`).
3. После старта оба сервиса должны стать **healthy**: в compose настроены health-check `curl /health` для `api` и `wget /healthz` для `web`.

Первая сборка занимает 3–8 минут, следующие быстрее за счёт кеша слоёв.

## 5. Почта: SPF, DKIM, DMARC

Без этих записей письма со ссылкой для входа будут уходить в спам. Значения выдаёт почтовый провайдер, ниже — общий вид:

| Запись | Тип | Пример |
|---|---|---|
| `@` | TXT | `v=spf1 include:<провайдер> ~all` |
| `<селектор>._domainkey` | TXT | ключ DKIM из панели провайдера |
| `_dmarc` | TXT | `v=DMARC1; p=none; rua=mailto:support@movingmotivators.ru` |

Заведите также ящик **`support@movingmotivators.ru`**. Он указан в Reply-To писем, в футере и в юридических документах.

## 6. Проверка после деплоя

- [ ] `https://movingmotivators.ru/health` отвечает `Healthy`.
- [ ] `https://www.movingmotivators.ru` перенаправляет на `https://movingmotivators.ru`.
- [ ] Сертификат валиден (замок в браузере).
- [ ] На `/login` вводите email: письмо приходит **во «Входящие»**, а не в спам, кнопка «Войти» работает.
- [ ] После входа под email из `ADMIN_EMAILS` в шапке есть ссылка «Админка».
- [ ] Создайте команду из 3 человек и опрос, пройдите его по ссылке с телефона, закройте опрос — на почту приходит письмо со сводкой.
- [ ] Создайте опрос на 12 человек: открывается экран оплаты, после «Оплатить» опрос запускается бесплатно, в `/admin` растёт счётчик «Нажали „Оплатить“».
- [ ] В бакете S3 есть хотя бы один бэкап.

Тестовые команды и опросы потом можно удалить в интерфейсе: ответы удаляются физически.

## Обновления

- **Автодеплой:** в ресурсе включите **Auto Deploy** (по умолчанию включён у GitHub App). Каждый push в выбранную ветку пересобирает и перезапускает приложение.
- **Вручную:** кнопка **Redeploy**.
- **Миграции** применяются при старте `api`. Инстанс `api` один, поэтому гонок миграций нет. Не масштабируйте `api` до нескольких реплик, пока не вынесете миграции в отдельный шаг.
- **Откат:** вкладка **Deployments** → выбрать прошлый успешный деплой → **Redeploy**. Учтите: миграции БД при этом назад не откатываются. Если новая миграция несовместима со старым кодом, восстановите базу из бэкапа.

## Восстановление из бэкапа

1. В ресурсе базы откройте **Backups** → выберите бэкап → **Restore** (или скачайте дамп из S3 и восстановите через `pg_restore`).
2. Перед восстановлением остановите приложение (**Stop**), чтобы во время восстановления не шли записи.
3. Запустите приложение снова.

## Monium: логи и трейсы

Если заданы переменные `MONIUM__*`, `api` отправляет телеметрию в [Monium](https://yandex.cloud/ru/docs/monium/collector/otlp-protocol) по OTLP/HTTP на `ingest.monium.yandex.cloud`:

- **Логи** — все записи Serilog с trace id, так что из лога можно перейти к трейсу. Метки: проект из `MONIUM__PROJECT`, `cluster` из `MONIUM__CLUSTER`, `service=api`.
- **Трейсы** — входящие HTTP-запросы, SQL-запросы к PostgreSQL и раз в минуту спан `maintenance` (закрытие опросов по сроку, очистка).
- **Не отправляются** IP-адреса, user-agent и query-строки: они вырезаются из спанов до отправки (анонимность участников, §6 SPEC). `/health` не трейсится.

Метрики не отправляются, только логи и трейсы.

**Подготовка в Yandex Cloud**
1. Создайте сервисный аккаунт и выдайте ему роль на **проект** Monium (или на каталог либо облако, где он находится). В документации роль встречается под двумя похожими именами: `monium.telemetry.writer` (роль) и `yc.monium.telemetry.write` (область действия ключа). Точное имя роли сверьте в консоли IAM.
2. Создайте для аккаунта **API-ключ** с областью действия `yc.monium.telemetry.write` (или `yc.monium.logs.write` + `yc.monium.traces.write`).
3. ID проекта возьмите в Monium.

**Как понять, что телеметрия доходит**

Главный признак — **лог `api` при старте и во время работы**:

| В логе | Что значит |
|---|---|
| `Monium: отправка включена, проект …, кластер production, сервис api` | Конфигурация принята. Если предупреждений ниже нет, данные доходят. |
| `Monium: отправка выключена — …` | Не задан ключ или проект, либо неверный эндпоинт; причина указана в строке. |
| `Телеметрия HttpRequestFailed: … Response: Unknown api key …` | Ошибка отправки трейсов, причина в ответе Monium. |
| `warn: Телеметрия (логи): … failed emitting a batch` | Ошибка отправки логов. |

Ошибки экспорта сами по себе молчат, поэтому их перехватывает [MoniumDiagnostics.cs](../backend/src/Motivators.Api/Infrastructure/MoniumDiagnostics.cs) и пишет в лог, одинаковые — не чаще раза в минуту. Приложение при этом не падает, логи по-прежнему видны в Coolify.

Ключ можно проверить и вручную, но **только по пути с `/otlp`**:

```bash
curl -s -w " [%{http_code}]\n" -X POST https://ingest.monium.yandex.cloud/otlp/v1/logs \
  -H "Content-Type: application/json" \
  -H "Authorization: Api-Key $MONIUM__APIKEY" \
  -H "x-monium-project: $MONIUM__PROJECT" \
  -d '{"resourceLogs":[]}'
```

- `{"partialSuccess":{…}} [200]` — ключ и проект в порядке.
- `{"code":7, "message":"authorization failed…"} [403]` — у аккаунта нет роли или у ключа нет нужной области действия, либо неверный проект.
- **`Hello! This is Monium. [200]` — проверка ничего не значит:** запрос ушёл не на тот путь (например, `/v1/logs` без `/otlp`), и ингест ответил заглушкой. По той же причине приложение не запускает отправку, если `Monium:Endpoint` не оканчивается на `/otlp`: экспортёр принял бы заглушку за успех, и данные пропадали бы молча.

**Если в Monium пусто, а предупреждений в логе нет** — скорее всего, вы смотрите не в тот кластер. Проверьте метку `cluster` в фильтре: в продакшене это `production` (`MONIUM__CLUSTER`), при локальном запуске (`ASPNETCORE_ENVIRONMENT=Development`) — `development`. Нужный кластер указан в строке `Monium: отправка включена…` при старте.

**Шум в логах уже убран:**
- успешные SQL-команды EF (`Executed DbCommand`) понижены до Debug в самом источнике; проверяет [DatabaseLogNoiseTests.cs](../backend/tests/Motivators.IntegrationTests/DatabaseLogNoiseTests.cs). SQL видно в трейсах;
- служебные записи исходящих HTTP-запросов (`System.Net.Http.HttpClient`) — только Warning и выше;
- в теле лога — готовый текст сообщения, а не шаблон с фигурными скобками. Шаблон дополнительно лежит в атрибуте `message_template.text`, по нему удобно группировать одинаковые события.

## Логи и диагностика

Логи `api` пишутся в stdout в формате JSON. Посмотреть их можно в ресурсе → **Logs** → сервис `api`. Логи nginx (`web`) не содержат IP-адресов и query-строк — это сделано намеренно, ради анонимности участников.

| Симптом | Причина и решение |
|---|---|
| `api` не стартует: `ConnectionStrings:Default не задан` или `Smtp:Host не задан` | Не заданы `DB_CONNECTION_STRING` или `SMTP_HOST` (шаг 4.3) — после добавления нажмите **Redeploy** |
| `api` не стартует, в логе `Failed to connect` | Неверная `DB_CONNECTION_STRING`, либо не включена опция **Connect To Predefined Network** (шаг 4.1) |
| `Pricing:CheckoutMode=… не поддерживается` | В MVP допустимо только `fake` |
| Вход не работает, после «Войти» снова просит войти | Сайт открыт не по HTTPS: cookie `__Host-session` требует Secure. Проверьте домен и сертификат |
| Мутации возвращают `403 bad_origin` | Сайт открыт не по адресу `App:BaseUrl` из appsettings.json (http/https, www, другой домен) |
| Письма не приходят | Проверьте `SMTP_*` и лог `api` (ошибки отправки пишутся туда). Частая причина — `SMTP_FROM` не совпадает с ящиком `SMTP_USERNAME` (Яндекс такое отклоняет). По умолчанию порт 465 с SSL; для 587 поменяйте `Smtp:Port` и `Smtp:Security` (`starttls`) в appsettings.json |
| Письма уходят в спам | Не настроены SPF/DKIM/DMARC (шаг 5) |
| Сборка падает с `Killed` / exit code 137 | Не хватает памяти при сборке — добавьте swap или увеличьте RAM |
| `429 Слишком много запросов` при тестах | Сработали лимиты: 20 запросов ссылки на IP в час, 5 ссылок на один email в час, 10 отправок ответов на IP в минуту |
