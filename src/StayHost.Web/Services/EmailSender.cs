using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace StayHost.Web.Services;

/// <summary>
/// Where outgoing mail goes. Filled from the <c>Email:</c> section of
/// configuration; <c>Email__Password</c> is meant to arrive as an environment
/// variable rather than sitting in appsettings.json.
/// </summary>
public class EmailSettings
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }

    /// <summary>The address recipients see, and reply to.</summary>
    public string FromAddress { get; set; } = "no-reply@stayhost.vn";
    public string FromName { get; set; } = "StayHost";

    /// <summary>
    /// Port 465 is TLS from the first byte; 587 starts plain and upgrades. Most
    /// Vietnamese providers hand out 587, which is why it is the default.
    /// </summary>
    public bool UseImplicitTls { get; set; }

    /// <summary>
    /// Nothing is sent until a host is configured. Without this the platform
    /// would queue mail forever and look like it was working.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>What happened to one attempt at handing a message to a mail server.</summary>
public readonly record struct EmailResult(bool Ok, string? Error = null, int? StatusCode = null);

public interface IEmailSender
{
    bool CanSend { get; }
    Task<EmailResult> SendAsync(string toEmail, string toName, string subject, string body, CancellationToken ct);
}

/// <summary>
/// The real thing. One connection per batch is left to the caller: this opens,
/// sends and closes, which is slower but cannot leave a socket half-open across
/// a sweep that failed in the middle.
/// </summary>
public class SmtpEmailSender(EmailSettings settings, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public bool CanSend => settings.IsConfigured;

    public async Task<EmailResult> SendAsync(
        string toEmail, string toName, string subject, string body, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        try
        {
            var security = settings.UseImplicitTls
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(settings.Host, settings.Port, security, ct);

            if (!string.IsNullOrWhiteSpace(settings.User))
                await client.AuthenticateAsync(settings.User, settings.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            return new EmailResult(true);
        }
        catch (SmtpCommandException e)
        {
            // The receiving server said no and said why. The status code decides
            // whether this is worth trying again (docs: EmailDelivery.IsPermanent).
            log.LogWarning(e, "SMTP refused mail to {To}: {Status}", toEmail, e.StatusCode);
            return new EmailResult(false, e.Message, (int)e.StatusCode);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A timeout, a DNS failure, a certificate problem: nothing was
            // refused, so nothing is permanent.
            log.LogWarning(e, "Could not hand mail to {To} over to the server.", toEmail);
            return new EmailResult(false, e.Message);
        }
    }
}

/// <summary>
/// What runs when no mail server is configured. It does not pretend to send:
/// the message stays queued and the log says exactly why, so a deployment
/// missing its credentials looks broken rather than quiet.
/// </summary>
public class UnconfiguredEmailSender(ILogger<UnconfiguredEmailSender> log) : IEmailSender
{
    public bool CanSend => false;

    public Task<EmailResult> SendAsync(
        string toEmail, string toName, string subject, string body, CancellationToken ct)
    {
        log.LogWarning(
            "Chưa cấu hình Email:Host — thư \"{Subject}\" gửi cho {To} vẫn nằm trong hàng đợi.",
            subject, toEmail);

        return Task.FromResult(new EmailResult(false, "Chưa cấu hình máy chủ gửi thư."));
    }
}
