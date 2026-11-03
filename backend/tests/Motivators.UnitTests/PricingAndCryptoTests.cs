using Microsoft.Extensions.Options;
using Motivators.Application.Auth;
using Motivators.Application.Common;
using Motivators.Application.Pricing;
using Motivators.Application.Results;
using Motivators.Application.Surveys;
using Motivators.Domain;

namespace Motivators.UnitTests;

public class PricingTests
{
    private static PricingService Create(int free = 8, int price = 299) =>
        new(Options.Create(new PricingOptions { FreeMaxParticipants = free, SurveyPriceRub = price }));

    [Theory]
    [InlineData(2, 0)]
    [InlineData(8, 0)]
    [InlineData(9, 299)]
    [InlineData(12, 299)]
    [InlineData(200, 299)]
    public void Free_up_to_8_then_299(int participants, int expected) =>
        Assert.Equal(expected, Create().GetSurveyPrice(participants));

    [Fact]
    public void Threshold_and_price_from_config()
    {
        var p = Create(free: 5, price: 499);
        Assert.Equal(0, p.GetSurveyPrice(5));
        Assert.Equal(499, p.GetSurveyPrice(6));
        Assert.Equal(new PricingDto(5, 499), p.Get());
    }
}

public class CryptoTests
{
    [Fact]
    public void Token_is_32_bytes_base64url()
    {
        var t = Crypto.NewToken();
        Assert.Equal(43, t.Length);
        Assert.DoesNotContain('+', t);
        Assert.DoesNotContain('/', t);
        Assert.DoesNotContain('=', t);
        Assert.NotEqual(t, Crypto.NewToken());
    }

    [Fact]
    public void Public_code_is_10_base62()
    {
        var c = Crypto.NewPublicCode();
        Assert.Equal(10, c.Length);
        Assert.Matches("^[0-9A-Za-z]{10}$", c);
    }

    [Fact]
    public void Respondent_hash_depends_on_salt()
    {
        var key = Guid.NewGuid().ToString();
        var s1 = Crypto.NewSalt();
        var s2 = Crypto.NewSalt();
        Assert.Equal(Crypto.RespondentHash(s1, key), Crypto.RespondentHash(s1, key));
        Assert.NotEqual(Crypto.RespondentHash(s1, key), Crypto.RespondentHash(s2, key));
        Assert.Equal(32, Crypto.Sha256("x").Length);
    }

    [Theory]
    [InlineData("/app", "/app")]
    [InlineData("/app/surveys/1?tab=x", "/app/surveys/1?tab=x")]
    [InlineData("https://evil.com", null)]
    [InlineData("//evil.com", null)]
    [InlineData("/\\evil.com", null)]
    [InlineData("", null)]
    public void Return_url_only_relative(string input, string? expected) =>
        Assert.Equal(expected, AuthService.SanitizeReturnUrl(input));

    [Theory]
    [InlineData("  User@Example.COM ", "user@example.com")]
    public void Email_normalization(string input, string expected) => Assert.Equal(expected, AuthService.NormalizeEmail(input));

    [Theory]
    [InlineData("a@b.ru", true)]
    [InlineData("not-an-email", false)]
    [InlineData("a@b", false)]
    public void Email_validation(string email, bool ok) => Assert.Equal(ok, AuthService.IsValidEmail(email));
}

public class MiscTests
{
    [Fact]
    public void Header_text_color_dark_only_for_light_cards()
    {
        var dark = MethodologyCatalog.Current.Motivators
            .Where(m => Colors.TextOn(m.AccentColor) == "#1A1A1A").Select(m => m.Key).ToHashSet();
        Assert.Equal(new HashSet<string> { "acceptance", "order" }, dark);
    }

    [Fact]
    public void Default_title_uses_russian_month()
    {
        var t = SurveyService.DefaultTitle(SurveyTypes.MovingMotivators, new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Движущие мотиваторы · ноябрь 2026", t);
    }

    [Fact]
    public void Compare_motivators_shift_by_name_case_insensitive()
    {
        var keys = MethodologyCatalog.Current.Motivators.Select(m => m.Key).ToArray();
        var opts = new ResultsOptions();
        var settings = new SurveySettings { Changes = [] };
        ResponseInput<MotivatorsPayload> R(string name, string[] ranking) =>
            new(Guid.CreateVersion7(), name, new MotivatorsPayload { Ranking = ranking.ToList() }, null, default);

        var a = MotivatorsResultsCalculator.Calculate(MethodologyCatalog.Current, settings, false, [R("Иван", keys)], opts);
        var swapped = keys.ToArray();
        (swapped[0], swapped[9]) = (swapped[9], swapped[0]);
        var b = MotivatorsResultsCalculator.Calculate(MethodologyCatalog.Current, settings, false, [R("иван ", swapped)], opts);

        var cmp = HistoryCalculator.CompareMotivators(new SurveyRef(Guid.NewGuid(), "A", null, 1), a, false,
            new SurveyRef(Guid.NewGuid(), "B", null, 1), b, false);
        Assert.Equal(-9, cmp.Cards.Single(c => c.Key == keys[9]).Delta);
        Assert.Equal(9, cmp.Cards.Single(c => c.Key == keys[0]).Delta);
        var p = Assert.Single(cmp.Participants!);
        Assert.Equal(-9, p.Shift[keys[9]]);
        Assert.Equal(0, p.Shift[keys[5]]);
    }
}
