using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 TK-01, TK-02 and TK-03 — signing up, proving it, and the age gate.</summary>
public class IdentityTests
{
    private static readonly DateOnly Today = new(2026, 8, 6);

    /* ------------------------------------------------------------- TK-01 */

    [Fact]
    public void A_phone_number_is_accepted_in_the_shapes_people_type_it()
    {
        foreach (var typed in new[]
                 {
                     "0912345678", "0912 345 678", "0912.345.678",
                     "+84912345678", "84912345678", "912345678"
                 })
        {
            Assert.Equal("0912345678", Identity.NormalisePhone(typed));
        }
    }

    [Fact]
    public void Something_that_is_not_a_phone_number_is_refused()
    {
        Assert.Null(Identity.NormalisePhone("123"));
        Assert.Null(Identity.NormalisePhone("00912345678"));      // no second leading zero
        Assert.Null(Identity.NormalisePhone("09123456789012"));
        Assert.Null(Identity.NormalisePhone("khong-phai-so"));
        Assert.Null(Identity.NormalisePhone(null));
        Assert.Null(Identity.NormalisePhone(""));
    }

    [Fact]
    public void An_email_has_to_look_like_one()
    {
        Assert.True(Identity.LooksLikeEmail("ban@vidu.vn"));
        Assert.False(Identity.LooksLikeEmail("ban@vidu"));         // no dot after the @
        Assert.False(Identity.LooksLikeEmail("@vidu.vn"));
        Assert.False(Identity.LooksLikeEmail("a@b@vidu.vn"));
        Assert.False(Identity.LooksLikeEmail("ban vidu@x.vn"));
        Assert.False(Identity.LooksLikeEmail(null));
    }

    [Fact]
    public void Either_an_email_or_a_phone_is_enough_to_sign_up()
    {
        var birthday = Today.AddYears(-25);

        Assert.True(Identity.CanRegister("ban@vidu.vn", null, "matkhau123", "Nguyễn A", birthday, Today, false).Ok);
        Assert.True(Identity.CanRegister(null, "0912345678", "matkhau123", "Nguyễn A", birthday, Today, false).Ok);
        Assert.True(Identity.CanRegister("ban@vidu.vn", "0912345678", "matkhau123", "Nguyễn A", birthday, Today, false).Ok);
    }

    [Fact]
    public void Signing_up_with_neither_is_refused()
    {
        var check = Identity.CanRegister(null, null, "matkhau123", "Nguyễn A", Today.AddYears(-25), Today, false);

        Assert.False(check.Ok);
        Assert.Equal(Identity.Refusal.NoIdentifier, check.Reason);
    }

    [Fact]
    public void An_identifier_already_in_use_says_which_one()
    {
        var email = Identity.CanRegister("ban@vidu.vn", null, "matkhau123", "A", Today.AddYears(-25), Today, true);
        var phone = Identity.CanRegister(null, "0912345678", "matkhau123", "A", Today.AddYears(-25), Today, true);

        Assert.Equal(Identity.Refusal.Taken, email.Reason);
        Assert.Contains("Email", email.Message);
        Assert.Contains("Số điện thoại", phone.Message);
    }

    [Fact]
    public void A_short_password_or_a_missing_name_still_stops_it()
    {
        var birthday = Today.AddYears(-25);

        Assert.Equal(Identity.Refusal.WeakPassword,
            Identity.CanRegister("ban@vidu.vn", null, "ngan", "A", birthday, Today, false).Reason);
        Assert.Equal(Identity.Refusal.NoName,
            Identity.CanRegister("ban@vidu.vn", null, "matkhau123", "  ", birthday, Today, false).Reason);
    }

    /* ------------------------------------------------------------- TK-03 */

    [Fact]
    public void Age_is_counted_on_the_day_not_by_calendar_year()
    {
        var birthday = new DateOnly(2008, 8, 7);

        // Eighteen tomorrow is seventeen today, whatever the year subtraction says.
        Assert.Equal(17, Identity.AgeOn(birthday, Today));
        Assert.Equal(18, Identity.AgeOn(birthday, Today.AddDays(1)));
    }

    [Fact]
    public void Nobody_under_eighteen_gets_an_account()
    {
        var seventeen = Today.AddYears(-18).AddDays(1);
        var eighteen = Today.AddYears(-18);

        Assert.False(Identity.IsOldEnough(seventeen, Today));
        Assert.True(Identity.IsOldEnough(eighteen, Today));

        Assert.Equal(Identity.Refusal.TooYoung,
            Identity.CanRegister("ban@vidu.vn", null, "matkhau123", "A", seventeen, Today, false).Reason);
    }

    [Fact]
    public void A_birthday_is_required_rather_than_assumed()
    {
        Assert.Equal(Identity.Refusal.NoBirthday,
            Identity.CanRegister("ban@vidu.vn", null, "matkhau123", "A", null, Today, false).Reason);
    }

    /* ---------------------------------------------------------- the code */

    [Fact]
    public void The_code_is_six_digits_and_lives_ten_minutes()
    {
        Assert.Equal(6, Identity.CodeLength);
        Assert.Equal(10, Identity.CodeLifetime.TotalMinutes);
    }

    [Fact]
    public void A_code_stops_working_when_its_time_is_up()
    {
        var sent = new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc);
        var expires = sent + Identity.CodeLifetime;

        Assert.False(Identity.CodeExpired(expires, sent.AddMinutes(9)));
        Assert.True(Identity.CodeExpired(expires, sent.AddMinutes(10)));
    }

    [Fact]
    public void Guessing_runs_out_after_five_tries()
    {
        Assert.False(Identity.OutOfAttempts(4));
        Assert.True(Identity.OutOfAttempts(5));
    }

    [Fact]
    public void Resending_has_a_cooldown_so_nobody_gets_spammed()
    {
        var sent = new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc);

        Assert.True(Identity.ResendTooSoon(sent, sent.AddSeconds(59)));
        Assert.False(Identity.ResendTooSoon(sent, sent.AddSeconds(60)));
    }

    /* ------------------------------------------------------------- TK-02 */

    [Fact]
    public void Only_the_three_providers_the_spec_names_exist()
    {
        Assert.Equal(3, Enum.GetValues<ExternalProvider>().Length);

        Assert.Equal("Google", Identity.ProviderLabel(ExternalProvider.Google));
        Assert.Equal("Apple", Identity.ProviderLabel(ExternalProvider.Apple));
        Assert.Equal("Facebook", Identity.ProviderLabel(ExternalProvider.Facebook));
    }

    [Fact]
    public void Both_identifier_kinds_have_wording_of_their_own()
    {
        Assert.Equal("email", Identity.KindLabel(IdentifierKind.Email));
        Assert.Equal("số điện thoại", Identity.KindLabel(IdentifierKind.Phone));
    }
}
