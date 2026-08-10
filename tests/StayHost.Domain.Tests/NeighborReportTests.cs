namespace StayHost.Domain.Tests;

/// <summary>docs/01 AT-03 — the neighbour report channel's rules.</summary>
public class NeighborReportTests
{
    [Fact]
    public void A_complete_report_validates()
    {
        Assert.Null(NeighborReports.Validate("12 Nguyễn Huệ, Đà Nẵng", "Tiệc ồn ào tới 2 giờ sáng nhiều đêm liền."));
    }

    [Fact]
    public void A_thin_location_or_detail_is_refused()
    {
        Assert.NotNull(NeighborReports.Validate("x", "Tiệc ồn ào tới khuya nhiều đêm."));
        Assert.NotNull(NeighborReports.Validate("12 Nguyễn Huệ", "ồn"));
    }

    [Theory]
    [InlineData("noise", NeighborConcern.Noise)]
    [InlineData("Safety", NeighborConcern.Safety)]
    [InlineData("PARTY", NeighborConcern.Party)]
    public void Concerns_parse_case_insensitively(string input, NeighborConcern expected)
    {
        Assert.True(NeighborReports.TryParseConcern(input, out var c));
        Assert.Equal(expected, c);
    }

    [Fact]
    public void An_unknown_concern_does_not_parse()
    {
        Assert.False(NeighborReports.TryParseConcern("bogus", out _));
        Assert.False(NeighborReports.TryParseConcern("99", out _));
    }
}
