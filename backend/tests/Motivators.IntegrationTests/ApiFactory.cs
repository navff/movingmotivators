using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Motivators.Application.Email;
using Testcontainers.PostgreSql;

namespace Motivators.IntegrationTests;

public class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public EmailMessage LastTo(string email) => Sent.Last(m => m.To == email);
}

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.ru";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public CapturingEmailSender Emails { get; } = new();

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Db:MigrateOnStartup", "true");
        builder.UseSetting("Background:Enabled", "false");
        builder.UseSetting("Logging:Json", "false");
        builder.UseSetting("App:BaseUrl", "https://localhost");
        builder.UseSetting("App:AdminEmails:0", AdminEmail);
        builder.UseSetting("RateLimits:AuthLinkPerIpPerHour", "1000");
        builder.UseSetting("RateLimits:PublicSubmitPerIpPerMinute", "1000");
        builder.UseSetting("Auth:LinksPerEmailPerHour", "100");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(Emails);
        });
    }

    public HttpClient Anonymous() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    /// <summary>Полный вход по magic link: запрос → письмо → verify → согласие.</summary>
    public async Task<HttpClient> LoginAsync(string email, bool acceptConsent = true)
    {
        var client = Anonymous();
        (await client.PostAsJsonAsync("/api/auth/request-link", new { email })).EnsureSuccessStatusCode();
        var token = ExtractToken(Emails.LastTo(email).Text);
        (await client.PostAsJsonAsync("/api/auth/verify", new { token })).EnsureSuccessStatusCode();
        if (acceptConsent)
            (await client.PatchAsJsonAsync("/api/me", new { consentVersion = "org-1" })).EnsureSuccessStatusCode();
        return client;
    }

    public static string ExtractToken(string text) =>
        Regex.Match(text, @"token=([A-Za-z0-9_\-]+)").Groups[1].Value;
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
