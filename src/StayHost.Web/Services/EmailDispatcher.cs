using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Drains the email queue. Every service that wants to write to somebody adds a
/// row to <see cref="EmailMessage"/> inside its own transaction; this is the one
/// place those rows are handed to a mail server. Which failures are retried and
/// when is decided in <see cref="EmailDelivery"/>, not here.
/// </summary>
public class EmailDispatcher(StayHostDbContext db, IEmailSender sender, ILogger<EmailDispatcher> log)
{
    public sealed record Result(int Sent, int Retrying, int GivenUp)
    {
        public bool Any => Sent + Retrying + GivenUp > 0;
        public override string ToString() => $"{Sent} gửi được, {Retrying} chờ thử lại, {GivenUp} bỏ cuộc";
    }

    public async Task<Result> SweepAsync(CancellationToken ct)
    {
        // With no server configured an attempt would only burn through the retry
        // schedule and mark good mail as failed. The queue holds everything, and
        // it all goes out once Email:Host is set.
        if (!sender.CanSend) return new(0, 0, 0);

        var now = DateTime.UtcNow;

        var due = await db.EmailMessages
            // Attempts == 0 with no schedule is a fresh message; a scheduled one
            // is due when its time has come. NextAttemptAt null with attempts on
            // the clock means the message was given up on, and stays given up.
            .Where(m => m.SentAt == null && !m.Undeliverable
                && ((m.Attempts == 0 && m.NextAttemptAt == null) || m.NextAttemptAt <= now))
            .OrderBy(m => m.Id)
            .Take(50)
            .ToListAsync(ct);

        int sent = 0, retrying = 0, givenUp = 0;

        foreach (var mail in due)
        {
            var result = await sender.SendAsync(mail.ToEmail, mail.ToName, mail.Subject, mail.Body, ct);
            mail.Attempts++;

            if (result.Ok)
            {
                mail.SentAt = DateTime.UtcNow;
                mail.NextAttemptAt = null;
                mail.Error = null;
                sent++;
                continue;
            }

            mail.Error = result.Error;

            if (EmailDelivery.IsPermanent(result.StatusCode))
            {
                // The receiving server will say the same no every time; asking
                // again only costs the sending reputation the sign-in codes need.
                mail.Undeliverable = true;
                mail.NextAttemptAt = null;
                givenUp++;
            }
            else if (EmailDelivery.ShouldRetry(result.StatusCode, mail.Attempts))
            {
                mail.NextAttemptAt = EmailDelivery.NextAttemptAt(DateTime.UtcNow, mail.Attempts);
                retrying++;
            }
            else
            {
                mail.NextAttemptAt = null;
                mail.Error = EmailDelivery.GiveUpNotice(mail.Attempts);
                givenUp++;
            }
        }

        if (due.Count > 0) await db.SaveChangesAsync(ct);

        return new(sent, retrying, givenUp);
    }
}

/// <summary>
/// Runs the sweep every fifteen seconds — not a minute, because the first thing
/// this queue carries is the six-digit sign-in code, and whoever asked for it is
/// sitting there watching their inbox.
/// </summary>
public class EmailWorker(IServiceProvider services, ILogger<EmailWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
                var result = await dispatcher.SweepAsync(stoppingToken);
                if (result.Any) log.LogInformation("Gửi thư: {Result}.", result);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Email sweep failed; next tick will try again.");
            }
        }
    }
}
