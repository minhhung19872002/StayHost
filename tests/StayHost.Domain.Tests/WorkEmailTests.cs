namespace StayHost.Domain.Tests;

/// <summary>docs/01 TK-07 — which addresses may earn the work-email badge.</summary>
public class WorkEmailTests
{
    [Theory]
    [InlineData("nguyen@fpt.com.vn")]
    [InlineData("a.b@vingroup.net")]
    [InlineData("staff@bluestar.com.vn")]
    public void A_company_domain_is_eligible(string email)
    {
        Assert.True(WorkEmail.IsCompanyEmail(email));
        Assert.False(WorkEmail.IsFreeProvider(email));
    }

    [Theory]
    [InlineData("me@gmail.com")]
    [InlineData("Me@Yahoo.com")]
    [InlineData("x@outlook.com")]
    [InlineData("y@icloud.com")]
    public void A_free_consumer_mailbox_is_not_eligible(string email)
    {
        Assert.False(WorkEmail.IsCompanyEmail(email));
        Assert.True(WorkEmail.IsFreeProvider(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@nodomain.com")]
    [InlineData("nolocal@")]
    [InlineData("two @spaces.com")]
    [InlineData("dot@trailing.")]
    [InlineData("double@dots..com")]
    public void A_malformed_address_is_rejected(string email)
    {
        Assert.False(WorkEmail.IsCompanyEmail(email));
        Assert.Null(WorkEmail.Domain(email));
    }

    [Fact]
    public void Domain_is_lowercased_and_trimmed()
    {
        Assert.Equal("bluestar.com.vn", WorkEmail.Domain("  Staff@BlueStar.Com.VN "));
        Assert.Equal("staff@bluestar.com.vn", WorkEmail.Normalise("  Staff@BlueStar.Com.VN "));
    }
}
