# Движущие Мотиваторы: онлайн-тест

Веб-сервис для руководителей: создать команду, запустить опрос **«Движущие мотиваторы»** (Moving Motivators, Management 3.0) или **Health Check** (Squad Health Check, Spotify), разослать ссылку участникам и получить результаты с подсказками к обсуждению. Требования — [docs/SPEC.md](docs/SPEC.md).

| Часть | Стек |
|---|---|
| backend | .NET 10, ASP.NET Core (контроллеры), EF Core 10 + Npgsql, FluentValidation, Serilog, MailKit, Fluid |
| БД | PostgreSQL 17 (миграции EF Core, `jsonb`) |
| frontend | React 19, TypeScript, Vite, React Router, TanStack Query, dnd-kit, Recharts, Tailwind CSS 4 |
| деплой | Coolify (Docker Compose) на VPS Timeweb Cloud |

## Структура

```
backend/
  src/Motivators.Domain          сущности, MethodologyCatalog (карточки, категории, вопросы)
  src/Motivators.Application     сценарии, расчёт результатов (чистые функции), pricing, checkout
  src/Motivators.Infrastructure  DbContext, миграции, SMTP, шаблоны писем (Email/Templates/*.liquid)
  src/Motivators.Api             контроллеры, cookie-сессии, rate limiting, фоновые задачи
  tests/                         юнит- и интеграционные тесты (Testcontainers)
frontend/                        SPA + nginx.conf (статика и прокси /api → api:8080)
docs/                            SPEC, юр. документы (рендерятся на /terms, /privacy, /consent)
```

## Локальный запуск

**Всё в Docker** (нужен Docker Desktop):

```bash
docker compose -f docker-compose.dev.yml up --build
```

- сайт — http://localhost:8081
- письма (Mailpit) — http://localhost:8025 — сюда приходят ссылки для входа
- админ — `admin@example.com` (страница `/admin`)

**Для разработки с hot reload:**

```bash
docker compose -f docker-compose.dev.yml up db mailpit   # Postgres :5432, Mailpit :1025/:8025
cd backend/src/Motivators.Api && dotnet run              # API на http://localhost:8080, миграции применяются при старте
cd frontend && npm install && npm run dev                # http://localhost:5173, /api проксируется на :8080
```

В Development `App:BaseUrl = http://localhost:5173`, админ — `admin@example.com` (`appsettings.Development.json`).

## Тесты

```bash
cd backend
dotnet test tests/Motivators.UnitTests          # расчёт ММ/HC, влияние изменений, pricing, валидация payload, токены
dotnet test tests/Motivators.IntegrationTests   # нужен Docker: поднимает PostgreSQL 17 через Testcontainers
```

Интеграционные тесты покрывают: вход по magic link и онбординг, цикл «создать → ответить ×3 → закрыть → результаты» с письмом, путь > 8 участников (`draft` → checkout → `open`, ссылка не работает до оплаты), IDOR (чужой опрос/команда → 404), антибот, историю HC, удаление аккаунта, проверку `Origin`.

Проверка типов и сборка фронтенда: `cd frontend && npm run build`.

## Деплой в Coolify

1. **Сервер.** VPS Timeweb Cloud (ДЦ в РФ) с установленным Coolify. DNS: `movingmotivators.ru` и `www` → IP сервера.
2. **PostgreSQL.** *New Resource → Database → PostgreSQL 17*. Включите *Scheduled Backups*: ежедневно, хранение 14 дней, S3-хранилище Timeweb Cloud. Скопируйте внутреннюю строку подключения.
3. **Приложение.** *New Resource → Docker Compose* из Git-репозитория, файл `docker-compose.yml`, автодеплой по push.
   - В *Environment Variables* задайте переменные из [.env.example](.env.example) — как минимум `DB_CONNECTION_STRING`, `SMTP_*`, `APP_ADMIN_EMAILS`.
   - Домен `https://movingmotivators.ru` назначьте сервису **web** (порт 80). Coolify/Traefik выпустит сертификат Let's Encrypt.
   - Редирект `www` → корень: добавьте `https://www.movingmotivators.ru` к сервису web и включите в Coolify редирект на основной домен (*Redirect to non-www*).
4. **Почта.** У российского провайдера (Unisender Go, Яндекс 360 и т. п.) настройте SPF, DKIM и DMARC для домена и ящик `support@movingmotivators.ru`.
5. После деплоя проверьте `https://movingmotivators.ru/health` → `Healthy`.

Миграции применяются при старте `api` (`Db:MigrateOnStartup=true`; инстанс один, гонок нет). Логи — JSON в stdout, смотрятся в Coolify.

## Конфигурация

Основное — в [backend/src/Motivators.Api/appsettings.json](backend/src/Motivators.Api/appsettings.json); в Docker переопределяется env-переменными вида `Section__Key`.

| Секция | Что настраивает |
|---|---|
| `App` | `ProductName`, `BaseUrl`, `AdminEmails` (через запятую), `ShowEnglishCardNames` |
| `Pricing` | `FreeMaxParticipants` (8), `SurveyPriceRub` (299), `CheckoutMode` (`fake`; `yookassa` — фаза 1.5) |
| `Results` | пороги подсказок и светофора: `MmDividedStd`, `MmNegativeImpact`, `MmSplitShare`, `HcGreenMin`, `HcYellowMin` |
| `Surveys` | лимит ответов, минимальное время прохождения, сроки по умолчанию, окно переоткрытия, хранение черновиков |
| `Auth`, `RateLimits` | TTL ссылки, срок сессии, лимиты запросов |
| `Smtp`, `Legal` | почта и реквизиты ИП |

## Заметки по реализации

- **Оплата — имитация (fake door).** Опрос > 8 участников создаётся как `draft`; экран оплаты не запрашивает платёжных данных, «Оплатить» фиксирует `payment_clicked` и запускает опрос бесплатно. Реальная оплата подключается заменой `ICheckoutService` (`FakeCheckoutService` → `YooKassaCheckoutService`) по `Pricing:CheckoutMode`.
- **Анонимность.** В анонимном опросе не хранятся IP, имя и время (только дата); `respondentKey` хранится как SHA-256 с солью опроса. nginx и API не пишут IP и query-строки в логи.
- **Методики** — статический справочник `MethodologyCatalog` с версией; фронтенд берёт его из `/api/catalog` и `/api/public/surveys/{code}`.
- **Иллюстрации карточек** — `frontend/src/assets/motivators/{key}.png`; если положить рядом `{key}.svg` (экспорт из Figma), компонент возьмёт SVG.
- **Страница участника** `/s/:code` — отдельный лёгкий chunk: ~140 КБ JS gzip (бюджет — 200 КБ).
