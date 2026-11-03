using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Motivators.Application.Email;

namespace Motivators.Infrastructure.Email;

public class SmtpOptions
{
    public const string Section = "Smtp";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? Username { get; set; }
    public string? Password { get; set; }
    /// <summary>none | ssl | starttls | auto</summary>
    public string Security { get; set; } = "none";
    public string From { get; set; } = "no-reply@movingmotivators.ru";
    public string FromName { get; set; } = "Движущие Мотиваторы";
    public string ReplyTo { get; set; } = "support@movingmotivators.ru";
}

public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.FromName, o.From));
        mime.ReplyTo.Add(MailboxAddress.Parse(o.ReplyTo));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();

        var security = o.Security.ToLowerInvariant() switch
        {
            "ssl" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "auto" => SecureSocketOptions.Auto,
            _ => SecureSocketOptions.None,
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, security, ct);
        if (!string.IsNullOrEmpty(o.Username))
            await client.AuthenticateAsync(o.Username, o.Password ?? "", ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
        // адрес получателя — ПДн, в лог не пишем
        logger.LogInformation("Письмо «{Subject}» отправлено", message.Subject);
    }
}
