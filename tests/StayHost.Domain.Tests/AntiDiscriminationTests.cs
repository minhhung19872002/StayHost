namespace StayHost.Domain.Tests;

/// <summary>docs/01 AT-12 — screening host decline reasons for protected characteristics.</summary>
public class AntiDiscriminationTests
{
    [Fact]
    public void An_ordinary_reason_is_not_flagged()
    {
        Assert.Equal(AntiDiscrimination.Category.None, AntiDiscrimination.Screen("Nhà đang sửa, không nhận khách tuần này."));
        Assert.Equal(AntiDiscrimination.Category.None, AntiDiscrimination.Screen("Ngày đó tôi đã có khách khác."));
        Assert.False(AntiDiscrimination.IsFlagged(""));
        Assert.False(AntiDiscrimination.IsFlagged(null));
    }

    [Theory]
    [InlineData("Không cho người dân tộc thuê", AntiDiscrimination.Category.Origin)]
    [InlineData("Chỉ nhận khách không theo đạo", AntiDiscrimination.Category.Religion)]
    [InlineData("Nhà không phù hợp người khuyết tật", AntiDiscrimination.Category.Disability)]
    [InlineData("Không nhận khách có con nhỏ", AntiDiscrimination.Category.Family)]
    [InlineData("Không cho thuê cặp đồng tính", AntiDiscrimination.Category.Gender)]
    public void A_reason_leaning_on_a_protected_trait_is_flagged(string reason, AntiDiscrimination.Category expected)
    {
        Assert.Equal(expected, AntiDiscrimination.Screen(reason));
        Assert.True(AntiDiscrimination.IsFlagged(reason));
    }

    [Fact]
    public void Screening_ignores_case_and_diacritics()
    {
        Assert.True(AntiDiscrimination.IsFlagged("khong nhan nguoi DAN TOC"));
    }
}
