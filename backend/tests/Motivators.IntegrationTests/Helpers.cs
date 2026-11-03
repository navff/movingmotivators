using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Motivators.Domain;

namespace Motivators.IntegrationTests;

public static class Helpers
{
    public static readonly string[] Keys = MethodologyCatalog.Current.Motivators.Select(m => m.Key).ToArray();

    public static async Task<JsonNode> Json(this HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        return JsonNode.Parse(text)!;
    }

    public static async Task<JsonNode> OkJson(this Task<HttpResponseMessage> request)
    {
        var r = await request;
        if (!r.IsSuccessStatusCode)
            throw new Xunit.Sdk.XunitException($"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return await r.Json();
    }

    public static async Task<Guid> CreateTeamAsync(HttpClient c, string name, int size) =>
        Guid.Parse((await c.PostAsJsonAsync("/api/teams", new { name, size }).OkJson())["id"]!.GetValue<string>());

    public static long StartedAt() => DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds();

    public static Dictionary<string, int> Marks(params (string Key, int Value)[] marks)
    {
        var d = Keys.ToDictionary(k => k, _ => 0);
        foreach (var (k, v) in marks) d[k] = v;
        return d;
    }

    public static object MmResponse(string[] ranking, Dictionary<string, Dictionary<string, int>>? impact = null,
        string? key = null, string? name = null, string? comment = null, bool? consent = null) => new
    {
        respondentKey = key ?? Guid.NewGuid().ToString(),
        participantName = name,
        payload = new { ranking, impact },
        comment,
        consent,
        honeypot = "",
        startedAt = StartedAt(),
    };

    public static JsonSerializerOptions Web { get; } = new(JsonSerializerDefaults.Web);
}
