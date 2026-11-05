using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Motivators.Infrastructure.Data;
using static Motivators.IntegrationTests.Helpers;

namespace Motivators.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SurveyFlowTests(ApiFactory api)
{
    [Fact]
    public async Task Create_respond_x3_close_results()
    {
        var email = $"org-{Guid.NewGuid():N}@test.ru";
        var org = await api.LoginAsync(email);
        var teamId = await CreateTeamAsync(org, "Платформа", 6);

        var created = await org.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
        {
            type = "moving_motivators",
            declaredParticipants = 6,
            isAnonymous = true,
            changes = new[] { "Возврат в офис 3 дня в неделю", "Новая оргструктура" },
        }).OkJson();
        Assert.False(created["checkoutRequired"]!.GetValue<bool>());
        var survey = created["survey"]!;
        Assert.Equal("open", survey["status"]!.GetValue<string>());
        Assert.StartsWith("Движущие мотиваторы · ", survey["title"]!.GetValue<string>());
        var code = survey["publicCode"]!.GetValue<string>();
        var id = survey["id"]!.GetValue<string>();
        Assert.Equal(10, code.Length);

        var participant = api.Anonymous();
        var pub = await participant.GetAsync($"/api/public/surveys/{code}").OkJson();
        Assert.Equal("open", pub["status"]!.GetValue<string>());
        Assert.Equal(10, pub["cards"]!.AsArray().Count);
        Assert.Equal(2, pub["changes"]!.AsArray().Count);
        Assert.Equal("Платформа", pub["teamName"]!.GetValue<string>());

        var top = "freedom";
        var ranking = Keys.Where(k => k != top).Append(top).ToArray();
        var impact = new Dictionary<string, Dictionary<string, int>> { ["c1"] = Marks((top, -1)), ["c2"] = Marks((top, 1)) };

        var key1 = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await participant.PostAsJsonAsync($"/api/public/surveys/{code}/responses",
            MmResponse(ranking, impact, key1, comment: "Нужна гибкость"))).StatusCode);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.Created, (await participant.PostAsJsonAsync($"/api/public/surveys/{code}/responses",
                MmResponse(ranking, impact))).StatusCode);

        // повторная отправка с тем же ключом заменяет ответ
        var replace = await participant.PostAsJsonAsync($"/api/public/surveys/{code}/responses",
            MmResponse(ranking, impact, key1, comment: "Нужна гибкость, правда"));
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var mine = await participant.PostAsJsonAsync($"/api/public/surveys/{code}/responses/mine", new { respondentKey = key1 }).OkJson();
        Assert.Equal(1, mine["updatedCount"]!.GetValue<int>());

        // живые результаты, пока открыт
        var live = await org.GetAsync($"/api/surveys/{id}/results").OkJson();
        Assert.Equal(3, live["responseCount"]!.GetValue<int>());

        var closed = await org.PostAsync($"/api/surveys/{id}/close", null).OkJson();
        Assert.Equal("closed", closed["status"]!.GetValue<string>());
        Assert.True(closed["canReopen"]!.GetValue<bool>());

        var mail = api.Emails.LastTo(email);
        Assert.Contains("завершён", mail.Subject);
        Assert.Contains("Свобода", mail.Html);
        Assert.Contains("Возврат в офис", mail.Html); // самое болезненное изменение
        Assert.Contains($"/app/surveys/{id}", mail.Html);

        var results = await org.GetAsync($"/api/surveys/{id}/results").OkJson();
        Assert.Equal(3, results["responseCount"]!.GetValue<int>());
        Assert.Equal(top, results["top3"]![0]!.GetValue<string>());
        Assert.Equal("c1", results["mostPainfulChangeId"]!.GetValue<string>());
        Assert.Null(results["participants"]);
        Assert.Equal("high", results["prompts"]![0]!["priority"]!.GetValue<string>());
        var comment = results["comments"]![0]!;
        Assert.Null(comment["name"]);
        Assert.Null(comment["submittedOn"]);

        // закрытый опрос: публично — только статус, отправка — 410
        var after = await participant.GetAsync($"/api/public/surveys/{code}").OkJson();
        Assert.Equal("closed", after["status"]!.GetValue<string>());
        Assert.Null(after["cards"]);
        var late = await participant.PostAsJsonAsync($"/api/public/surveys/{code}/responses", MmResponse(ranking, impact));
        Assert.Equal(HttpStatusCode.Gone, late.StatusCode);
        Assert.Equal("survey_closed", (await late.Json())["errorCode"]!.GetValue<string>());

        // переоткрытие
        var reopened = await org.PostAsJsonAsync($"/api/surveys/{id}/reopen", new { closesAt = DateTimeOffset.UtcNow.AddDays(3) }).OkJson();
        Assert.Equal("open", reopened["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ratings_are_required_and_reach_the_report()
    {
        var org = await api.LoginAsync($"rate-{Guid.NewGuid():N}@test.ru");
        var teamId = await CreateTeamAsync(org, "Оценки", 4);
        var survey = (await org.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
        {
            type = "moving_motivators", declaredParticipants = 4, isAnonymous = true,
        }).OkJson())["survey"]!;
        var code = survey["publicCode"]!.GetValue<string>();
        var id = survey["id"]!.GetValue<string>();
        var p = api.Anonymous();

        var pub = await p.GetAsync($"/api/public/surveys/{code}").OkJson();
        Assert.Equal(5, pub["cards"]![0]!["levels"]!.AsArray().Count);

        // без оценок ответ не принимается
        var noRatings = await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            respondentKey = Guid.NewGuid().ToString(), payload = new { ranking = Keys }, honeypot = "", startedAt = StartedAt(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, noRatings.StatusCode);

        // самая важная карточка развита слабо
        var top = Keys[^1];
        var ratings = Ratings(4);
        ratings[top] = 2;
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses",
            MmResponse(Keys, key: key, ratings: ratings))).StatusCode);

        // оценки возвращаются участнику для правки ответа
        var mine = await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses/mine", new { respondentKey = key }).OkJson();
        Assert.Equal(2, mine["payload"]!["ratings"]![top]!.GetValue<int>());

        await org.PostAsync($"/api/surveys/{id}/close", null).OkJson();
        var results = await org.GetAsync($"/api/surveys/{id}/results").OkJson(); // из снимка
        var card = results["cards"]!.AsArray().Single(c => c!["key"]!.GetValue<string>() == top)!;
        Assert.Equal(2, card["avgRating"]!.GetValue<double>());
        Assert.Equal(1, card["ratingDistribution"]![1]!.GetValue<int>());
        Assert.Equal("low_rated_top_card", results["prompts"]![0]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task Survey_created_before_ratings_keeps_working_without_them()
    {
        var org = await api.LoginAsync($"v1-{Guid.NewGuid():N}@test.ru");
        var teamId = await CreateTeamAsync(org, "Старый опрос", 4);
        var survey = (await org.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
        {
            type = "moving_motivators", declaredParticipants = 4, isAnonymous = true,
        }).OkJson())["survey"]!;
        var code = survey["publicCode"]!.GetValue<string>();
        var id = survey["id"]!.GetValue<string>();

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Surveys.Where(s => s.PublicCode == code)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MethodologyVersion, 1));
        }

        var p = api.Anonymous();
        var pub = await p.GetAsync($"/api/public/surveys/{code}").OkJson();
        Assert.Null(pub["cards"]![0]!["levels"]);

        var answer = await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            respondentKey = Guid.NewGuid().ToString(), payload = new { ranking = Keys }, honeypot = "", startedAt = StartedAt(),
        });
        Assert.Equal(HttpStatusCode.Created, answer.StatusCode);

        var results = await org.GetAsync($"/api/surveys/{id}/results").OkJson();
        Assert.Null(results["cards"]![0]!["avgRating"]);
    }

    [Fact]
    public async Task Paid_survey_goes_draft_checkout_open()
    {
        var org = await api.LoginAsync($"paid-{Guid.NewGuid():N}@test.ru");
        var teamId = await CreateTeamAsync(org, "Большая команда", 12);
        var created = await org.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
        {
            type = "moving_motivators", declaredParticipants = 12, isAnonymous = true, changes = new[] { "A", "B" },
        }).OkJson();
        Assert.True(created["checkoutRequired"]!.GetValue<bool>());
        Assert.Equal(299, created["priceRub"]!.GetValue<int>());
        var survey = created["survey"]!;
        Assert.Equal("draft", survey["status"]!.GetValue<string>());
        var id = survey["id"]!.GetValue<string>();
        var code = survey["publicCode"]!.GetValue<string>();

        // до оплаты ссылка не работает
        var participant = api.Anonymous();
        Assert.Equal(HttpStatusCode.NotFound, (await participant.GetAsync($"/api/public/surveys/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await participant.PostAsJsonAsync($"/api/public/surveys/{code}/responses",
            MmResponse(Keys))).StatusCode);

        var summary = await org.PostAsync($"/api/surveys/{id}/checkout/view", null).OkJson();
        Assert.Equal(299, summary["amountRub"]!.GetValue<int>());
        Assert.Equal(12, summary["participants"]!.GetValue<int>());
        Assert.Equal("352826429326", summary["legalInn"]!.GetValue<string>());

        (await org.PostAsJsonAsync($"/api/surveys/{id}/checkout/method", new { method = "sbp" })).EnsureSuccessStatusCode();
        var bad = await org.PostAsJsonAsync($"/api/surveys/{id}/checkout", new { method = "bitcoin" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var checkout = await org.PostAsJsonAsync($"/api/surveys/{id}/checkout", new { method = "sbp" }).OkJson();
        Assert.Equal("launched_free", checkout["status"]!.GetValue<string>());
        Assert.Equal("open", checkout["survey"]!["status"]!.GetValue<string>());

        // повторный checkout невозможен
        Assert.Equal(HttpStatusCode.Conflict, (await org.PostAsJsonAsync($"/api/surveys/{id}/checkout", new { method = "sbp" })).StatusCode);

        var pub = await participant.GetAsync($"/api/public/surveys/{code}").OkJson();
        Assert.Equal("open", pub["status"]!.GetValue<string>());

        // админка видит payment_clicked
        var admin = await api.LoginAsync(ApiFactory.AdminEmail);
        var funnel = await admin.GetAsync("/api/admin/funnel").OkJson();
        Assert.True(funnel["paymentClicked"]!.GetValue<int>() >= 1);
        Assert.True(funnel["checkoutViewed"]!.GetValue<int>() >= 1);
        Assert.Contains(funnel["byMethod"]!.AsArray(), x => x!["key"]!.GetValue<string>() == "sbp");

        // не-админ в админку не попадает
        Assert.Equal(HttpStatusCode.Forbidden, (await org.GetAsync("/api/admin/funnel")).StatusCode);
    }

    [Fact]
    public async Task Idor_foreign_survey_and_team_are_404()
    {
        var owner = await api.LoginAsync($"owner-{Guid.NewGuid():N}@test.ru");
        var teamId = await CreateTeamAsync(owner, "Секретная", 5);
        var created = await owner.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
        {
            type = "health_check", declaredParticipants = 5, isAnonymous = false,
        }).OkJson();
        var id = created["survey"]!["id"]!.GetValue<string>();

        var intruder = await api.LoginAsync($"intruder-{Guid.NewGuid():N}@test.ru");
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/surveys/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/surveys/{id}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/surveys/{id}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/api/surveys/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/teams/{teamId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PatchAsJsonAsync($"/api/teams/{teamId}", new { name = "hacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsJsonAsync($"/api/teams/{teamId}/surveys",
            new { type = "health_check", declaredParticipants = 5, isAnonymous = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/teams/{teamId}/history")).StatusCode);
        Assert.Empty((await intruder.GetAsync("/api/teams").OkJson()).AsArray());

        // владелец по-прежнему видит
        (await owner.GetAsync($"/api/surveys/{id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_check_named_survey_with_history()
    {
        var org = await api.LoginAsync($"hc-{Guid.NewGuid():N}@test.ru");
        var teamId = await CreateTeamAsync(org, "HC", 4);
        var cats = new[] { "delivery", "fun", "speed" };

        for (var wave = 0; wave < 2; wave++)
        {
            var s = (await org.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
            {
                type = "health_check", title = $"Q{wave + 1}", declaredParticipants = 4, isAnonymous = false, categories = cats,
            }).OkJson())["survey"]!;
            var code = s["publicCode"]!.GetValue<string>();
            var p = api.Anonymous();
            var body = new
            {
                respondentKey = Guid.NewGuid().ToString(),
                participantName = "Пётр",
                payload = new { answers = cats.ToDictionary(c => c, _ => new { score = wave + 1, trend = 1, comment = (string?)null }) },
                consent = true,
                honeypot = "",
                startedAt = StartedAt(),
            };
            // без согласия — нельзя
            var noConsent = await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", body with { consent = false });
            Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", body)).StatusCode);
            await org.PostAsync($"/api/surveys/{s["id"]!.GetValue<string>()}/close", null).OkJson();

            var results = await org.GetAsync($"/api/surveys/{s["id"]!.GetValue<string>()}/results").OkJson();
            Assert.Equal("Пётр", results["participants"]![0]!["name"]!.GetValue<string>());
        }

        var history = await org.GetAsync($"/api/teams/{teamId}/history?type=health_check").OkJson();
        var waves = history["waves"]!.AsArray();
        Assert.Equal(2, waves.Count);
        Assert.Equal("red", waves[0]!["categories"]!["delivery"]!["color"]!.GetValue<string>());
        Assert.Equal("yellow", waves[1]!["categories"]!["delivery"]!["color"]!.GetValue<string>());
    }

    [Fact]
    public async Task Antibot_and_validation()
    {
        var org = await api.LoginAsync($"bot-{Guid.NewGuid():N}@test.ru");
        var teamId = await CreateTeamAsync(org, "T", 3);
        var code = (await org.PostAsJsonAsync($"/api/teams/{teamId}/surveys", new
        {
            type = "moving_motivators", declaredParticipants = 3, isAnonymous = true,
        }).OkJson())["survey"]!["publicCode"]!.GetValue<string>();
        var p = api.Anonymous();

        // слишком быстро
        var fast = await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", new
        {
            respondentKey = Guid.NewGuid().ToString(), payload = new { ranking = Keys }, honeypot = "",
            startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
        Assert.Equal("too_fast", (await fast.Json())["errorCode"]!.GetValue<string>());

        // неполное ранжирование
        var bad = await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", MmResponse(Keys.Take(9).ToArray()));
        Assert.Equal("validation_failed", (await bad.Json())["errorCode"]!.GetValue<string>());

        // honeypot — молча не сохраняем
        var bot = MmResponse(Keys);
        var botBody = System.Text.Json.JsonSerializer.SerializeToNode(bot)!;
        botBody["honeypot"] = "http://spam";
        (await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", botBody)).EnsureSuccessStatusCode();
        var survey = await org.GetAsync($"/api/teams/{teamId}").OkJson();
        Assert.Equal(0, survey["surveys"]![0]!["responseCount"]!.GetValue<int>());

        // ответов больше заявленного не блокируем
        for (var i = 0; i < 4; i++)
            (await p.PostAsJsonAsync($"/api/public/surveys/{code}/responses", MmResponse(Keys))).EnsureSuccessStatusCode();
        var id = survey["surveys"]![0]!["id"]!.GetValue<string>();
        Assert.True((await org.GetAsync($"/api/surveys/{id}").OkJson())["responsesOverDeclared"]!.GetValue<bool>());

        // анонимность нельзя поменять после ответов
        var lockResp = await org.PatchAsJsonAsync($"/api/surveys/{id}", new { isAnonymous = false });
        Assert.Equal(HttpStatusCode.Conflict, lockResp.StatusCode);
    }
}
