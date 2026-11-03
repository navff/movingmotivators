using System.Net;
using System.Net.Http.Json;

namespace Motivators.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthFlowTests(ApiFactory api)
{
    [Fact]
    public async Task Magic_link_login_onboarding_and_logout()
    {
        var email = $"lead-{Guid.NewGuid():N}@test.ru";
        var client = api.Anonymous();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);

        var req = await client.PostAsJsonAsync("/api/auth/request-link", new { email = "  " + email.ToUpperInvariant() + " " });
        Assert.Equal(HttpStatusCode.Accepted, req.StatusCode);

        var mail = api.Emails.LastTo(email);
        Assert.Contains("15 минут", mail.Text);
        var token = ApiFactory.ExtractToken(mail.Text);
        Assert.Equal(43, token.Length);

        var verify = await client.PostAsJsonAsync("/api/auth/verify", new { token }).OkJson();
        Assert.True(verify["isNew"]!.GetValue<bool>());
        Assert.Equal(email, verify["user"]!["email"]!.GetValue<string>());

        var me = await client.GetAsync("/api/me").OkJson();
        Assert.True(me["needsOnboarding"]!.GetValue<bool>());
        Assert.False(me["isAdmin"]!.GetValue<bool>());

        var updated = await client.PatchAsJsonAsync("/api/me", new { displayName = "Анна", consentVersion = "org-1" }).OkJson();
        Assert.False(updated["needsOnboarding"]!.GetValue<bool>());
        Assert.Equal("Анна", updated["displayName"]!.GetValue<string>());

        // токен одноразовый
        var reuse = await api.Anonymous().PostAsJsonAsync("/api/auth/verify", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal("invalid_token", (await reuse.Json())["errorCode"]!.GetValue<string>());

        (await client.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Request_link_always_202_even_for_garbage()
    {
        var r = await api.Anonymous().PostAsJsonAsync("/api/auth/request-link", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
    }

    [Fact]
    public async Task Return_url_is_preserved_only_when_relative()
    {
        var email = $"ret-{Guid.NewGuid():N}@test.ru";
        var c = api.Anonymous();
        await c.PostAsJsonAsync("/api/auth/request-link", new { email, returnUrl = "/app/surveys/123" });
        var v = await c.PostAsJsonAsync("/api/auth/verify", new { token = ApiFactory.ExtractToken(api.Emails.LastTo(email).Text) }).OkJson();
        Assert.Equal("/app/surveys/123", v["returnUrl"]!.GetValue<string>());

        await c.PostAsJsonAsync("/api/auth/request-link", new { email, returnUrl = "https://evil.com" });
        var v2 = await c.PostAsJsonAsync("/api/auth/verify", new { token = ApiFactory.ExtractToken(api.Emails.LastTo(email).Text) }).OkJson();
        Assert.Null(v2["returnUrl"]);
    }

    [Fact]
    public async Task Logout_all_kills_every_session()
    {
        var email = $"multi-{Guid.NewGuid():N}@test.ru";
        var a = await api.LoginAsync(email);
        var b = await api.LoginAsync(email);
        (await a.PostAsync("/api/auth/logout-all", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Foreign_origin_mutation_is_rejected()
    {
        var c = await api.LoginAsync($"csrf-{Guid.NewGuid():N}@test.ru");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/teams") { Content = JsonContent.Create(new { name = "X", size = 5 }) };
        req.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Account_deletion_via_confirmation_link()
    {
        var email = $"del-{Guid.NewGuid():N}@test.ru";
        var c = await api.LoginAsync(email);
        await Helpers.CreateTeamAsync(c, "Команда", 5);
        (await c.PostAsync("/api/me/delete-request", null)).EnsureSuccessStatusCode();
        var token = ApiFactory.ExtractToken(api.Emails.LastTo(email).Text);
        (await c.PostAsJsonAsync("/api/me/delete-confirm", new { token })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/me")).StatusCode);
    }
}
