using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/03 §10 — contact details are masked in messages until a booking is
/// confirmed. docs/01 ĐG-09 — a review carrying them is refused outright.
/// </summary>
public class ContentGuardTests
{
    [Theory]
    [InlineData("Gọi mình 0912345678 nhé")]
    [InlineData("SĐT: 0912 345 678")]
    [InlineData("+84 912 345 678")]
    [InlineData("Số mình là 0912.345.678")]
    public void Phone_numbers_are_found(string text)
    {
        Assert.True(ContentGuard.Inspect(text).HasPhone);
        Assert.DoesNotContain("912", ContentGuard.MaskContacts(text));
    }

    [Theory]
    [InlineData("mail mình: an@example.com")]
    [InlineData("an (at) example (dot) com")]
    public void Email_addresses_are_found(string text)
    {
        Assert.True(ContentGuard.Inspect(text).HasEmail);
        Assert.DoesNotContain("example", ContentGuard.MaskContacts(text));
    }

    [Theory]
    [InlineData("xem thêm https://example.com/villa")]
    [InlineData("vào www.villa-danang.vn nhé")]
    [InlineData("trang villadanang.com có giá tốt hơn")]
    public void Links_are_found(string text)
    {
        Assert.True(ContentGuard.Inspect(text).HasLink);
        Assert.Contains(ContentGuard.Mask, ContentGuard.MaskContacts(text));
    }

    [Theory]
    [InlineData("zalo: 0912345678")]
    [InlineData("Telegram @villadanang")]
    public void Messaging_handles_are_found(string text)
    {
        var finding = ContentGuard.Inspect(text);
        Assert.True(finding.HasHandle || finding.HasPhone);
        Assert.Contains(ContentGuard.Mask, ContentGuard.MaskContacts(text));
    }

    [Fact]
    public void Ordinary_text_is_left_alone()
    {
        const string text = "Nhà rất sạch, chủ nhà nhiệt tình, đi bộ 5 phút ra biển.";

        Assert.False(ContentGuard.Inspect(text).Any);
        Assert.Equal(text, ContentGuard.MaskContacts(text));
    }

    [Fact]
    public void Numbers_that_are_not_phone_numbers_survive()
    {
        const string text = "Phòng 302, tầng 3, có 2 giường đôi.";
        Assert.Equal(text, ContentGuard.MaskContacts(text));
    }

    [Fact]
    public void Masking_never_touches_the_stored_text()
    {
        const string original = "Gọi 0912345678";
        var masked = ContentGuard.MaskContacts(original);

        Assert.NotEqual(original, masked);
        Assert.Equal("Gọi 0912345678", original);      // the input is untouched
    }

    /* ------------------------------------------------------------- reviews */

    [Fact]
    public void A_clean_review_passes()
    {
        Assert.True(ContentGuard.CheckReview("Chỗ nghỉ sạch sẽ, chủ nhà rất chu đáo.").Ok);
    }

    [Fact]
    public void A_review_with_contact_details_is_refused_not_masked()
    {
        var result = ContentGuard.CheckReview("Liên hệ mình 0912345678 để đặt trực tiếp rẻ hơn");

        Assert.False(result.Ok);
        Assert.Contains("số điện thoại", result.Message);
    }

    [Fact]
    public void A_review_with_a_slur_is_refused()
    {
        var result = ContentGuard.CheckReview("Chủ nhà đúng là đồ chó, không bao giờ quay lại");

        Assert.False(result.Ok);
        Assert.Contains("xúc phạm", result.Message);
    }

    [Fact]
    public void The_refusal_names_only_what_it_actually_found()
    {
        var result = ContentGuard.CheckReview("Xem thêm ở villadanang.com nhé");

        Assert.False(result.Ok);
        Assert.Contains("đường liên kết", result.Message);
        Assert.DoesNotContain("số điện thoại", result.Message);
    }

    [Fact]
    public void Nothing_in_is_never_a_violation()
    {
        Assert.False(ContentGuard.Inspect(null).Any);
        Assert.True(ContentGuard.CheckReview("").Ok);
        Assert.Equal("", ContentGuard.MaskContacts(null));
    }
}
