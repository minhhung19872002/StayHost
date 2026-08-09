namespace StayHost.Domain.Tests;

/// <summary>docs/01 AT-01 — the pre-publish review gate.</summary>
public class ListingModerationTests
{
    [Fact]
    public void Approved_and_published_is_the_only_publicly_visible_state()
    {
        Assert.True(ListingModeration.IsPubliclyVisible(true, ListingReviewStatus.Approved));
        Assert.False(ListingModeration.IsPubliclyVisible(false, ListingReviewStatus.Approved));
        Assert.False(ListingModeration.IsPubliclyVisible(true, ListingReviewStatus.Pending));
        Assert.False(ListingModeration.IsPubliclyVisible(true, ListingReviewStatus.Rejected));
    }

    [Fact]
    public void With_the_gate_off_a_new_listing_is_approved_at_once()
    {
        Assert.Equal(ListingReviewStatus.Approved,
            ListingModeration.StatusForNew(isPublished: true, requireApproval: false));
    }

    [Fact]
    public void With_the_gate_on_a_newly_published_listing_waits_for_review()
    {
        Assert.Equal(ListingReviewStatus.Pending,
            ListingModeration.StatusForNew(isPublished: true, requireApproval: true));
    }

    [Fact]
    public void A_draft_never_enters_the_queue_even_with_the_gate_on()
    {
        // Nothing to review until it is actually published.
        Assert.Equal(ListingReviewStatus.Approved,
            ListingModeration.StatusForNew(isPublished: false, requireApproval: true));
    }

    [Fact]
    public void Editing_an_approved_listing_keeps_it_live()
    {
        // AT-01 is about new listings appearing, not freezing every edit.
        Assert.Equal(ListingReviewStatus.Approved,
            ListingModeration.StatusOnSave(ListingReviewStatus.Approved, isPublished: true, requireApproval: true));
    }

    [Fact]
    public void Republishing_a_rejected_listing_is_a_resubmission()
    {
        Assert.Equal(ListingReviewStatus.Pending,
            ListingModeration.StatusOnSave(ListingReviewStatus.Rejected, isPublished: true, requireApproval: true));
    }

    [Fact]
    public void A_pending_listing_stays_pending_while_being_edited()
    {
        Assert.Equal(ListingReviewStatus.Pending,
            ListingModeration.StatusOnSave(ListingReviewStatus.Pending, isPublished: true, requireApproval: true));
    }

    [Fact]
    public void With_the_gate_off_saving_never_holds_a_listing()
    {
        Assert.Equal(ListingReviewStatus.Approved,
            ListingModeration.StatusOnSave(ListingReviewStatus.Rejected, isPublished: true, requireApproval: false));
        Assert.Equal(ListingReviewStatus.Approved,
            ListingModeration.StatusOnSave(ListingReviewStatus.Pending, isPublished: true, requireApproval: false));
    }
}
