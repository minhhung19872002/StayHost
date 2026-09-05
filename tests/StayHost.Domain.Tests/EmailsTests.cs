namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 TK-09, the server half — the mail frame and the secret-bearing
/// templates in the reader's own language, and the drift guard that stops a
/// ninth language from reaching the picker without reaching the mails.
/// </summary>
public class EmailsTests
{
    /* ------------------------------------------------------- drift guard */

    [Fact]
    public void Every_language_the_picker_offers_has_a_hand_translated_frame()
    {
        // Translations.Targets is the one list the picker, the machine
        // translator and Locales all share. A language added there without a
        // frame here would silently mail that person Vietnamese — this is the
        // test that turns silence into a red build.
        foreach (var (code, label) in Translations.Targets)
            Assert.True(Emails.Covers(code), $"No email frame for '{code}' ({label}).");
    }

    [Fact]
    public void Every_language_has_its_own_secret_templates_too()
    {
        // The switch expressions fall back to Vietnamese for an unknown code —
        // which is correct for null, and drift for a language the picker sells.
        // A language's own template never equals the Vietnamese one.
        foreach (var (code, label) in Translations.Targets.Where(t => t.Code != "vi"))
        {
            Assert.NotEqual(Emails.OtpSubject("vi"), Emails.OtpSubject(code));
            Assert.NotEqual(Emails.OtpBody("vi", "123456", 10), Emails.OtpBody(code, "123456", 10));
            Assert.NotEqual(Emails.ResetSubject("vi"), Emails.ResetSubject(code));
            Assert.NotEqual(Emails.ResetBody("vi", "https://x"), Emails.ResetBody(code, "https://x"));
        }
    }

    /* ------------------------------------------------------------ parity */

    [Fact]
    public void The_vietnamese_frame_is_what_BuildEmailBody_always_produced()
    {
        // Byte-for-byte: the queued mails in every running database carry this
        // exact frame, and "vi" readers must not see their mails change shape.
        var body = Emails.Compose("vi", "Ngọc", "Đơn đã xác nhận", "Chi tiết ở đây.",
            "https://staylio.vn/trips/1");

        Assert.Equal(
            "Chào Ngọc,\n\nĐơn đã xác nhận\n\nChi tiết ở đây.\n\n" +
            "Xem chi tiết: https://staylio.vn/trips/1\n\n— Đội ngũ Staylio",
            body);
    }

    [Fact]
    public void Null_and_unknown_languages_mean_vietnamese()
    {
        var vi = Emails.Compose("vi", "A", "T", "B", null);
        Assert.Equal(vi, Emails.Compose(null, "A", "T", "B", null));
        Assert.Equal(vi, Emails.Compose("xx", "A", "T", "B", null));
        Assert.Equal(vi, Emails.Compose("  ", "A", "T", "B", null));
        Assert.Equal(Emails.OtpSubject("vi"), Emails.OtpSubject(null));
    }

    [Fact]
    public void No_url_means_no_link_line_at_all()
    {
        // A link line with nothing after it is exactly the dead-link lesson of
        // CLAUDE.md §4 — when there is no address, the line itself goes.
        var body = Emails.Compose("en", "Anna", "Confirmed", "Details.", null);
        Assert.DoesNotContain("View details:", body);
    }

    /* ------------------------------------------ the name is IN the frame */

    [Fact]
    public void The_name_sits_inside_the_greeting_not_after_it()
    {
        // Korean and Japanese put the name before the honorific. Gluing
        // "greeting + name" broke exactly this way once (\"Message Binn\").
        Assert.StartsWith("민지님,", Emails.Compose("ko", "민지", "T", "B", null));
        Assert.StartsWith("健太 様", Emails.Compose("ja", "健太", "T", "B", null));
        Assert.StartsWith("Chào Ngọc,", Emails.Compose("vi", "Ngọc", "T", "B", null));
    }

    /* --------------------------------------------------- machine honesty */

    [Fact]
    public void A_machine_translated_mail_says_so_and_a_hand_written_one_does_not()
    {
        var machine = Emails.Compose("en", "A", "T", "B", null, machineTranslated: true);
        var hand = Emails.Compose("en", "A", "T", "B", null);

        Assert.Contains("Automatically translated", machine);
        Assert.DoesNotContain("Automatically translated", hand);
    }

    [Fact]
    public void Vietnamese_never_carries_a_translation_notice()
    {
        // There is nothing to disclose: vi is the original.
        var body = Emails.Compose("vi", "A", "T", "B", null, machineTranslated: true);
        Assert.DoesNotContain("dịch", body, StringComparison.OrdinalIgnoreCase);
    }

    /* ------------------------------------------------------- otp secrecy */

    [Fact]
    public void No_language_puts_digits_in_the_otp_subject()
    {
        // Subjects show on lock screens and in mail logs — the same guard that
        // watches the Vietnamese subject watches all eight.
        foreach (var (code, _) in Translations.Targets)
            Assert.False(EmailDelivery.SubjectLeaksCode(Emails.OtpSubject(code)),
                $"OTP subject for '{code}' contains digits.");
    }

    [Fact]
    public void The_code_and_the_link_survive_every_translation()
    {
        // The one thing a secret-bearing template must do in all languages.
        foreach (var (code, _) in Translations.Targets)
        {
            Assert.Contains("482913", Emails.OtpBody(code, "482913", 10));
            Assert.Contains("https://staylio.vn/reset?t=abc",
                Emails.ResetBody(code, "https://staylio.vn/reset?t=abc"));
        }
    }
}
