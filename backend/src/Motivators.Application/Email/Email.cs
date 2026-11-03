namespace Motivators.Application.Email;

public record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public static class EmailTemplates
{
    public const string Login = "login";
    public const string SurveyClosed = "survey_closed";
    public const string DeleteAccount = "delete_account";
}

public interface IEmailRenderer
{
    /// <summary>Рендерит {name}.html.liquid и {name}.txt.liquid; модель доступна в шаблоне как поля.</summary>
    (string Html, string Text) Render(string template, object model);
}
