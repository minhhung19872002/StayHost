using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §2.5 — booking without an account, and paying the host at the door.
/// Two features that arrived together because they answer the same guest: the
/// one who does not want to hand a card to a website.
/// </summary>
public class GuestCheckoutTests
{
    private static GuestCheckout.Contact Ok(string? name = "Trần Minh", string? email = "minh@vidu.vn",
        string? phone = "0901234567") => new(name, email, phone);

    private static GuestCheckout.Check Book(
        GuestCheckout.Contact? contact = null,
        bool photo = false, bool verified = false,
        bool credit = false, bool coupon = false, bool offer = false) =>
        GuestCheckout.CanBookAnonymously(contact ?? Ok(), photo, verified, credit, coupon, offer);

    [Fact]
    public void A_stranger_with_a_name_an_email_and_a_phone_may_book()
    {
        Assert.True(Book().Ok);
    }

    [Fact]
    public void All_three_contact_details_are_required()
    {
        // The email is not a formality: it carries the reference, and the
        // reference is the only way back to the booking afterwards.
        Assert.Equal(GuestCheckout.Refusal.MissingName, Book(Ok(name: " ")).Reason);
        Assert.Equal(GuestCheckout.Refusal.MissingName, Book(Ok(name: "A")).Reason);
        Assert.Equal(GuestCheckout.Refusal.MissingEmail, Book(Ok(email: "khong-phai-email")).Reason);
        Assert.Equal(GuestCheckout.Refusal.MissingEmail, Book(Ok(email: null)).Reason);
        Assert.Equal(GuestCheckout.Refusal.MissingPhone, Book(Ok(phone: "12")).Reason);
        Assert.Equal(GuestCheckout.Refusal.MissingPhone, Book(Ok(phone: null)).Reason);
    }

    [Fact]
    public void A_host_who_asked_for_a_profile_does_not_get_a_stranger()
    {
        // docs/01 ĐP-10 — neither can ever be true of somebody with no account,
        // so this is a refusal rather than a fallback to request-to-book: the
        // host turned these on precisely so they would not have to decide.
        Assert.Equal(GuestCheckout.Refusal.HostRequiresAccount, Book(photo: true).Reason);
        Assert.Equal(GuestCheckout.Refusal.HostRequiresAccount, Book(verified: true).Reason);
        Assert.Contains("đăng nhập", Book(verified: true).Message.ToLowerInvariant());
    }

    [Fact]
    public void Money_that_belongs_to_an_account_is_refused_by_name()
    {
        // Silently dropping a promo code the guest typed reads as a broken site,
        // which is why each of these says what to do about it.
        Assert.Equal(GuestCheckout.Refusal.NeedsAccountForMoney, Book(credit: true).Reason);
        Assert.Equal(GuestCheckout.Refusal.NeedsAccountForMoney, Book(coupon: true).Reason);
        Assert.Equal(GuestCheckout.Refusal.OfferIsPersonal, Book(offer: true).Reason);
    }

    [Fact]
    public void Who_they_are_is_asked_before_what_the_host_wants()
    {
        // Somebody who typed nothing should be told to type their name, not that
        // the host wants a verified guest — one is fixable in the form in front
        // of them, the other means going somewhere else.
        Assert.Equal(GuestCheckout.Refusal.MissingName,
            Book(Ok(name: null), photo: true, credit: true).Reason);
    }

    /* ------------------------------------------------------------ lookup */

    [Fact]
    public void A_booking_is_found_only_with_both_the_reference_and_the_email()
    {
        const string reference = "SH1A2B3C4D";

        Assert.True(GuestCheckout.Matches("minh@vidu.vn", reference, "minh@vidu.vn", reference));

        // A reference travels alone in a forwarded subject line, so it is not
        // enough on its own.
        Assert.False(GuestCheckout.Matches("minh@vidu.vn", reference, "nguoikhac@vidu.vn", reference));
        Assert.False(GuestCheckout.Matches("minh@vidu.vn", "SH0000000", "minh@vidu.vn", reference));
    }

    [Fact]
    public void The_reference_is_typed_off_a_printout_so_case_and_spaces_are_forgiven()
    {
        const string reference = "SH1A2B3C4D";

        Assert.True(GuestCheckout.Matches("minh@vidu.vn", " sh1a2b3c4d ", "minh@vidu.vn", reference));
        Assert.True(GuestCheckout.Matches("minh@vidu.vn", "SH1A 2B3C 4D", " Minh@Vidu.VN ", reference));
    }

    [Fact]
    public void A_booking_with_no_email_on_it_cannot_be_looked_up_at_all()
    {
        // Otherwise an empty stored address would match an empty typed one, and
        // every account-made booking would answer to a blank form.
        Assert.False(GuestCheckout.Matches(null, "SH1", "", "SH1"));
        Assert.False(GuestCheckout.Matches("", "SH1", "", "SH1"));
    }
}
