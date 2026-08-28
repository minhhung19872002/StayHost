using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 TK-06 — proving who somebody is with a document and a selfie.</summary>
public class IdentityChecksTests
{
    // The shape UploadsController actually answers with. These constants used to
    // read "/uploads/7-front.jpg" — the public avatar folder — which is not a
    // place an identity photo has been stored since they were moved outside the
    // web root. The tests passed, and every real submission was refused.
    private const string Front = "/api/identity-files/7-1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d.jpg";
    private const string Back = "/api/identity-files/7-2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e.jpg";
    private const string Selfie = "/api/identity-files/7-3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f.png";

    private static IdentityChecks.Check Submit(
        IdentityCheck? latest = null,
        IdentityDocument document = IdentityDocument.NationalId,
        string? front = Front, string? back = Back, string? selfie = Selfie) =>
        IdentityChecks.CanSubmit(latest, document, front, back, selfie);

    [Fact]
    public void A_complete_first_submission_is_accepted()
    {
        Assert.True(Submit().Ok);
    }

    [Fact]
    public void A_passport_is_not_asked_for_a_back_page()
    {
        Assert.False(IdentityChecks.NeedsBackImage(IdentityDocument.Passport));
        Assert.True(Submit(document: IdentityDocument.Passport, back: null).Ok);
    }

    [Fact]
    public void An_identity_card_without_its_back_is_refused()
    {
        Assert.True(IdentityChecks.NeedsBackImage(IdentityDocument.NationalId));
        Assert.False(Submit(back: null).Ok);
    }

    [Fact]
    public void Every_photo_has_to_be_a_photo_this_platform_stored()
    {
        // An off-site address is somebody telling the reviewer's browser what
        // to fetch.
        Assert.False(Submit(front: "https://example.com/id.jpg").Ok);
        Assert.False(Submit(selfie: "javascript:alert(1)").Ok);
        Assert.False(Submit(front: null).Ok);
        Assert.False(Submit(selfie: "").Ok);
    }

    [Fact]
    public void The_public_uploads_folder_is_not_where_identity_papers_live()
    {
        // docs/08 §4 — /uploads/ is served by the static file middleware to
        // anyone holding the URL, with no role check and no audit line. An
        // identity photo accepted from there would be readable by the whole
        // internet, so the address itself has to be refused.
        Assert.False(IdentityChecks.IsIdentityUpload("/uploads/7-front.jpg"));
        Assert.False(Submit(front: "/uploads/7-front.jpg").Ok);
    }

    [Fact]
    public void Only_names_the_file_route_could_serve_are_accepted()
    {
        Assert.True(IdentityChecks.IsIdentityUpload(Front));
        Assert.True(IdentityChecks.IsIdentityUpload(Selfie));

        // No path of its own, no climbing out, no other extension: a URL that
        // passes here has to be one IdentityFilesController can find.
        Assert.False(IdentityChecks.IsIdentityUpload("/api/identity-files/../appsettings.json"));
        Assert.False(IdentityChecks.IsIdentityUpload("/api/identity-files/sub/7-1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d.jpg"));
        Assert.False(IdentityChecks.IsIdentityUpload("/api/identity-files/7-1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d.svg"));
        Assert.False(IdentityChecks.IsIdentityUpload("/api/identity-files/notanid.jpg"));
        Assert.False(IdentityChecks.IsIdentityUpload("/api/identity-files/"));
        Assert.False(IdentityChecks.IsIdentityUpload(null));
    }

    [Fact]
    public void The_same_photo_sent_twice_is_not_two_pieces_of_evidence()
    {
        Assert.False(Submit(selfie: Front).Ok);
        Assert.False(Submit(back: Front).Ok);
    }

    [Fact]
    public void Nobody_queues_two_submissions_at_once()
    {
        var pending = new IdentityCheck { Status = IdentityCheckStatus.Pending };
        Assert.False(Submit(pending).Ok);
    }

    [Fact]
    public void Somebody_already_verified_is_not_asked_to_do_it_again()
    {
        var approved = new IdentityCheck { Status = IdentityCheckStatus.Approved };
        var result = Submit(approved);
        Assert.False(result.Ok);
        Assert.Contains("đã được xác minh", result.Message);
    }

    [Fact]
    public void A_refusal_may_be_answered_with_a_new_submission()
    {
        var rejected = new IdentityCheck { Status = IdentityCheckStatus.Rejected, Note = "Ảnh mờ" };
        Assert.True(Submit(rejected).Ok);
    }

    [Fact]
    public void Only_the_last_four_characters_of_a_document_number_are_kept()
    {
        Assert.Equal("6789", IdentityChecks.Last4("012345 6789"));
        Assert.Equal("C123", IdentityChecks.Last4("b-c123"));
        Assert.Equal("12", IdentityChecks.Last4("12"));
        Assert.Null(IdentityChecks.Last4("   "));
        Assert.Null(IdentityChecks.Last4(null));
    }

    [Fact]
    public void Every_document_and_status_has_wording_of_its_own()
    {
        var seen = new HashSet<string>();
        foreach (IdentityDocument doc in Enum.GetValues<IdentityDocument>())
            Assert.True(seen.Add(IdentityChecks.DocumentLabel(doc)), $"{doc} reuses a label");

        seen.Clear();
        foreach (IdentityCheckStatus status in Enum.GetValues<IdentityCheckStatus>())
        {
            Assert.True(seen.Add(IdentityChecks.StatusLabel(status)), $"{status} reuses a label");
            Assert.NotEmpty(IdentityChecks.BadgeClass(status));
        }
    }
}
