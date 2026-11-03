using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Application.Email;
using Motivators.Domain;

namespace Motivators.Application.Auth;

public record UserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsAdmin,
    string? ConsentVersion,
    DateTimeOffset? ConsentAt,
    bool NeedsOnboarding,
    string CurrentConsentVersion);

public record VerifyResult(UserDto User, bool IsNew, string SessionToken, DateTimeOffset SessionExpiresAt, string? ReturnUrl);

public record RequestLinkRequest(string Email, string? ReturnUrl);

public record VerifyRequest(string Token);

public record UpdateMeRequest(string? DisplayName, string? ConsentVersion);

public class AuthService(
    IAppDbContext db,
    IEmailSender email,
    IEmailRenderer renderer,
    Tracking tracking,
    IOptions<AuthOptions> authOptions,
    IOptions<AppOptions> appOptions,
    IOptions<LegalOptions> legalOptions,
    TimeProvider clock,
    ILogger<AuthService> logger)
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly AppOptions _app = appOptions.Value;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email) =>
        email.Length is > 3 and <= 254 && MailAddress.TryCreate(email, out var a) && a.Address == email && email.Contains('.');

    /// <summary>Только относительные пути внутри сайта.</summary>
    public static string? SanitizeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") && url.Length <= 500
            ? url
            : null;

    /// <summary>Ответ снаружи всегда одинаковый; невалидный email и превышение лимита молча игнорируются.</summary>
    public async Task RequestLinkAsync(RequestLinkRequest req, CancellationToken ct)
    {
        var address = NormalizeEmail(req.Email ?? "");
        if (!IsValidEmail(address)) return;
        var now = clock.GetUtcNow();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address, ct);
        if (user == null)
        {
            user = new User { Email = address, CreatedAt = now };
            db.Users.Add(user);
        }
        else
        {
            var recent = await db.LoginTokens.CountAsync(t =>
                t.UserId == user.Id && t.Purpose == TokenPurpose.Login && t.CreatedAt > now.AddHours(-1), ct);
            if (recent >= _auth.LinksPerEmailPerHour)
            {
                logger.LogWarning("Превышен лимит ссылок для пользователя {UserId}", user.Id);
                return;
            }
        }

        var token = Crypto.NewToken();
        db.LoginTokens.Add(new LoginToken
        {
            UserId = user.Id,
            TokenHash = Crypto.Sha256(token),
            Purpose = TokenPurpose.Login,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_auth.LoginTokenTtlMinutes),
            ReturnUrl = SanitizeReturnUrl(req.ReturnUrl),
        });
        await db.SaveChangesAsync(ct);

        var (html, text) = renderer.Render(EmailTemplates.Login, new
        {
            productName = _app.ProductName,
            url = _app.Url($"/auth/verify?token={token}"),
            ttlMinutes = _auth.LoginTokenTtlMinutes,
        });
        await email.SendAsync(new EmailMessage(address, $"Вход в «{_app.ProductName}»", html, text), ct);
    }

    public async Task<VerifyResult> VerifyAsync(string token, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var lt = await ConsumeTokenAsync(token, TokenPurpose.Login, ct);
        var user = lt.User;
        var isNew = user.ConsentAt == null;
        user.EmailVerifiedAt ??= now;
        user.LastLoginAt = now;

        var sessionToken = Crypto.NewToken();
        var session = new Session
        {
            UserId = user.Id,
            SessionHash = Crypto.Sha256(sessionToken),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(_auth.SessionDays),
        };
        db.Sessions.Add(session);
        tracking.Audit(user.Id, "login", "user", user.Id);
        await db.SaveChangesAsync(ct);
        return new VerifyResult(ToDto(user), isNew, sessionToken, session.ExpiresAt, lt.ReturnUrl);
    }

    /// <summary>Проверяет сессию, продлевает скользящий срок. null — сессии нет или истекла.</summary>
    public async Task<(User User, DateTimeOffset ExpiresAt, bool Renewed)?> ValidateSessionAsync(string sessionToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = Crypto.Sha256(sessionToken);
        var session = await db.Sessions.Include(s => s.User).FirstOrDefaultAsync(s => s.SessionHash == hash, ct);
        if (session == null || session.ExpiresAt <= now) return null;

        // продлеваем не чаще раза в час, чтобы не писать в БД на каждый запрос
        var renewed = false;
        if (now - session.LastSeenAt > TimeSpan.FromHours(1))
        {
            session.LastSeenAt = now;
            session.ExpiresAt = now.AddDays(_auth.SessionDays);
            await db.SaveChangesAsync(ct);
            renewed = true;
        }
        return (session.User, session.ExpiresAt, renewed);
    }

    public async Task LogoutAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return;
        var hash = Crypto.Sha256(sessionToken);
        await db.Sessions.Where(s => s.SessionHash == hash).ExecuteDeleteAsync(ct);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken ct) =>
        await db.Sessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct) =>
        ToDto(await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw AppException.NotFound());

    public async Task<UserDto> UpdateMeAsync(Guid userId, UpdateMeRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw AppException.NotFound();
        if (req.DisplayName != null)
        {
            var name = req.DisplayName.Trim();
            if (name.Length > 100) throw AppException.Validation("Имя — не длиннее 100 символов");
            user.DisplayName = name.Length == 0 ? null : name;
        }
        if (req.ConsentVersion != null)
        {
            if (req.ConsentVersion != legalOptions.Value.OrganizerConsentVersion)
                throw AppException.Validation("Неактуальная версия согласия");
            user.ConsentVersion = req.ConsentVersion;
            user.ConsentAt = clock.GetUtcNow();
        }
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task RequestDeleteAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        var now = clock.GetUtcNow();
        var token = Crypto.NewToken();
        db.LoginTokens.Add(new LoginToken
        {
            UserId = user.Id, TokenHash = Crypto.Sha256(token), Purpose = TokenPurpose.DeleteAccount,
            CreatedAt = now, ExpiresAt = now.AddMinutes(_auth.LoginTokenTtlMinutes),
        });
        await db.SaveChangesAsync(ct);
        var (html, text) = renderer.Render(EmailTemplates.DeleteAccount, new
        {
            productName = _app.ProductName,
            url = _app.Url($"/account/delete?token={token}"),
            ttlMinutes = _auth.LoginTokenTtlMinutes,
        });
        await email.SendAsync(new EmailMessage(user.Email, "Подтверждение удаления аккаунта", html, text), ct);
    }

    /// <summary>Физически удаляет пользователя и всё связанное (каскадом в БД).</summary>
    public async Task ConfirmDeleteAsync(string token, CancellationToken ct)
    {
        var lt = await ConsumeTokenAsync(token, TokenPurpose.DeleteAccount, ct);
        var userId = lt.UserId;
        tracking.Audit(null, "delete_account", "user", userId);
        db.Users.Remove(lt.User);
        await db.SaveChangesAsync(ct);
    }

    private async Task<LoginToken> ConsumeTokenAsync(string token, string purpose, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = Crypto.Sha256(token ?? "");
        var lt = await db.LoginTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
        if (lt == null || lt.UsedAt != null || lt.ExpiresAt <= now)
            throw new AppException(400, ErrorCodes.InvalidToken, "Ссылка недействительна или устарела. Запросите новую.");
        lt.UsedAt = now;
        return lt;
    }

    private UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, _app.IsAdmin(u.Email), u.ConsentVersion, u.ConsentAt,
        u.ConsentVersion != legalOptions.Value.OrganizerConsentVersion, legalOptions.Value.OrganizerConsentVersion);
}
