namespace StayHost.Domain;

/// <summary>
/// docs/07 §2.5 — booking without an account, the way every other booking site
/// on the Vietnamese market lets somebody book.
///
/// The platform has always half-supported this: <see cref="Booking.SessionId"/>
/// is on every row, the trips list reads bookings by session, and signing in
/// adopts whatever the session was holding. Only the endpoint that creates a
/// booking refused, so the scaffolding sat there unreachable.
///
/// What a stranger cannot have is anything that hangs off an account: a balance,
/// a per-person promo limit, a private offer, a saved card. Each of those is
/// refused by name rather than ignored, because silently dropping a promo code
/// the guest typed reads as the site being broken.
/// </summary>
public static class GuestCheckout
{
    public enum Refusal
    {
        None = 0,
        /// <summary>docs/01 ĐP-10 — the host asked that nobody stay without a photo or a verified identity.</summary>
        HostRequiresAccount,
        /// <summary>Balance, gift cards and per-person promo limits are properties of an account.</summary>
        NeedsAccountForMoney,
        /// <summary>docs/01 ĐP-17 — an offer was extended to one person, who has an account.</summary>
        OfferIsPersonal,
        MissingName,
        MissingEmail,
        MissingPhone
    }

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    /// <summary>How the platform reaches somebody with no account.</summary>
    public readonly record struct Contact(string? Name, string? Email, string? Phone);

    /// <summary>
    /// Whether this booking can be made without signing in.
    ///
    /// The order is the order a guest meets the problems in: who they are first,
    /// then what the host demands, then what they were trying to spend.
    /// </summary>
    public static Check CanBookAnonymously(
        Contact contact,
        bool listingRequiresPhoto,
        bool listingRequiresVerified,
        bool usesCredit,
        bool usesCoupon,
        bool usesOffer)
    {
        if (string.IsNullOrWhiteSpace(contact.Name) || contact.Name.Trim().Length < 2)
            return Check.Fail(Refusal.MissingName, "Cần họ tên người nhận phòng.");

        if (!Identity.LooksLikeEmail(contact.Email))
            return Check.Fail(Refusal.MissingEmail,
                "Cần email để gửi xác nhận đặt chỗ và mã đơn.");

        if (Identity.NormalisePhone(contact.Phone) is null)
            return Check.Fail(Refusal.MissingPhone,
                "Cần số điện thoại để chủ nhà liên hệ khi bạn tới.");

        // docs/01 ĐP-10 — these are the host's hard preconditions, and neither can
        // be true of somebody with no account. Falling back to a request to book
        // would not help: the host set them precisely to avoid deciding.
        if (listingRequiresPhoto || listingRequiresVerified)
            return Check.Fail(Refusal.HostRequiresAccount,
                "Chủ nhà chỗ này chỉ nhận khách đã có hồ sơ. Hãy đăng nhập hoặc tạo tài khoản để đặt.");

        if (usesCredit || usesCoupon)
            return Check.Fail(Refusal.NeedsAccountForMoney,
                "Số dư và mã giảm giá gắn với tài khoản. Hãy đăng nhập để dùng, hoặc bỏ đi để đặt ngay.");

        if (usesOffer)
            return Check.Fail(Refusal.OfferIsPersonal,
                "Ưu đãi riêng chỉ dùng được bằng tài khoản đã nhận nó.");

        return Check.Pass;
    }

    /// <summary>
    /// docs/01 ĐP-13 — the two things a guest with no account has to find their
    /// booking again with. The reference alone is not enough: it travels in an
    /// email subject line and gets forwarded.
    ///
    /// Matching is deliberately forgiving about case and spacing, because this is
    /// typed by hand off a printed confirmation.
    /// </summary>
    public static bool Matches(string? storedEmail, string? typedReference, string? typedEmail, string bookingReference)
    {
        if (string.IsNullOrWhiteSpace(storedEmail)) return false;

        var reference = (typedReference ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        if (!string.Equals(reference, bookingReference, StringComparison.OrdinalIgnoreCase)) return false;

        return string.Equals(
            (typedEmail ?? "").Trim(), storedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Said on the checkout when somebody is not signed in. It has to promise the
    /// thing that makes people willing: no account, and the booking is still
    /// findable afterwards.
    /// </summary>
    public const string Notice =
        "Bạn có thể đặt mà không cần tài khoản. Chúng tôi gửi mã đơn qua email; "
        + "dùng mã đơn và email đó để xem hoặc huỷ đặt chỗ bất cứ lúc nào.";
}
