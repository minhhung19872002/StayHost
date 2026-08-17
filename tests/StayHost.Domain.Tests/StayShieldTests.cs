using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/06 — the windows, the waiting, the ladder and the ceilings.</summary>
public class StayShieldTests
{
    private static readonly DateTime CheckIn = new(2026, 9, 10, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CheckOut = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    private static Shield.Request Ask(
        ShieldCase kind, DateTime now, DateTime? contacted = null, bool urgent = false,
        int evidence = 2, DateTime? nextGuest = null, decimal claimed = 1_000_000m,
        bool openCase = false, bool onPlatform = true, string? thirdParty = "Hàng xóm căn 704") =>
        new()
        {
            Kind = kind,
            Now = now,
            CheckInAt = CheckIn,
            CheckOutAt = CheckOut,
            HostContactedAt = contacted,
            Urgent = urgent,
            EvidenceCount = evidence,
            NextGuestArrivesAt = nextGuest,
            Claimed = claimed,
            AlreadyHasOpenCase = openCase,
            PaidThroughPlatform = onPlatform,
            ThirdParty = thirdParty
        };

    /* ------------------------------------------------------------- §2.2 */

    [Fact]
    public void A_guest_who_waited_the_hour_may_file()
    {
        var now = CheckIn.AddHours(2);

        Assert.True(Shield.CanFile(Ask(ShieldCase.K2, now, contacted: now.AddHours(-1))).Ok);
    }

    [Fact]
    public void Filing_before_talking_to_the_host_is_refused()
    {
        Assert.Equal(
            Shield.Refusal.HostNotContacted,
            Shield.CanFile(Ask(ShieldCase.K2, CheckIn.AddHours(2))).Reason);
    }

    [Fact]
    public void The_waiting_period_is_an_hour_for_lockouts_and_three_for_the_rest()
    {
        var now = CheckIn.AddHours(4);

        Assert.True(Shield.CanFile(Ask(ShieldCase.K2, now, contacted: now.AddMinutes(-61))).Ok);
        Assert.Equal(
            Shield.Refusal.StillWaitingOnHost,
            Shield.CanFile(Ask(ShieldCase.K3, now, contacted: now.AddMinutes(-61))).Reason);
        Assert.True(Shield.CanFile(Ask(ShieldCase.K3, now, contacted: now.AddHours(-3))).Ok);
    }

    [Fact]
    public void Danger_or_strangers_inside_skips_the_wait_entirely()
    {
        Assert.True(Shield.CanFile(Ask(ShieldCase.K4, CheckIn.AddMinutes(10), urgent: true)).Ok);
    }

    [Fact]
    public void Nothing_can_be_filed_before_check_in()
    {
        Assert.Equal(
            Shield.Refusal.TooEarly,
            Shield.CanFile(Ask(ShieldCase.K3, CheckIn.AddHours(-1), urgent: true)).Reason);
    }

    [Fact]
    public void The_guest_window_closes_after_seventy_two_hours()
    {
        Assert.True(Shield.CanFile(Ask(ShieldCase.K3, CheckIn.AddHours(71), urgent: true)).Ok);
        Assert.Equal(
            Shield.Refusal.WindowClosed,
            Shield.CanFile(Ask(ShieldCase.K3, CheckIn.AddHours(73), urgent: true)).Reason);
    }

    [Fact]
    public void A_case_without_a_photo_is_not_a_case()
    {
        Assert.Equal(
            Shield.Refusal.NoEvidence,
            Shield.CanFile(Ask(ShieldCase.K4, CheckIn.AddHours(2), urgent: true, evidence: 0)).Reason);
    }

    [Fact]
    public void A_host_cancellation_is_never_filed_by_hand()
    {
        Assert.Equal(
            Shield.Refusal.WrongSide,
            Shield.CanFile(Ask(ShieldCase.K1, CheckIn.AddHours(1), urgent: true)).Reason);
    }

    [Fact]
    public void Nothing_booked_off_the_platform_is_covered()
    {
        Assert.Equal(
            Shield.Refusal.BookedOffPlatform,
            Shield.CanFile(Ask(ShieldCase.K2, CheckIn.AddHours(2), urgent: true, onPlatform: false)).Reason);
    }

    /* ------------------------------------------------------------- §3.4 */

    /// <summary>
    /// docs/06 §3.4, as the customer settled it on 17/08/2026: damage is raised
    /// while the guest is still at the door and paid in cash there. Two days
    /// later used to be fine and is not any more — by then the person who broke
    /// it is gone and the guest cannot see what they are being charged for.
    /// </summary>
    [Fact]
    public void Damage_is_raised_on_the_day_or_not_at_all()
    {
        var contacted = CheckOut.AddHours(1);

        Assert.True(Shield.CanFile(Ask(ShieldCase.C1, CheckOut.AddHours(2), contacted: contacted)).Ok);
        Assert.Equal(
            Shield.Refusal.TooEarly,
            Shield.CanFile(Ask(ShieldCase.C1, CheckOut.AddHours(-1), contacted: contacted)).Reason);
        Assert.Equal(
            Shield.Refusal.WindowClosed,
            Shield.CanFile(Ask(ShieldCase.C1, CheckOut.AddDays(2), contacted: contacted)).Reason);
    }

    /// <summary>
    /// The fortnight survives for the two kinds nobody could have settled at the
    /// door: income lost when the next booking is cancelled, and a neighbour who
    /// only notices later.
    /// </summary>
    [Fact]
    public void Lost_income_and_a_neighbours_claim_keep_the_fortnight()
    {
        var contacted = CheckOut.AddHours(1);

        Assert.True(Shield.CanFile(Ask(ShieldCase.C3, CheckOut.AddDays(10), contacted: contacted)).Ok);
        Assert.True(Shield.CanFile(Ask(ShieldCase.C4, CheckOut.AddDays(10), contacted: contacted)).Ok);
        Assert.Equal(
            Shield.Refusal.WindowClosed,
            Shield.CanFile(Ask(ShieldCase.C3, CheckOut.AddDays(15), contacted: contacted)).Reason);

        Assert.Equal(Shield.DamageReportWindow, Shield.ReportWindow(ShieldCase.C1));
        Assert.Equal(Shield.HostReportWindow, Shield.ReportWindow(ShieldCase.C3));
    }

    [Fact]
    public void Once_the_next_guest_is_in_nobody_can_say_who_did_it()
    {
        // Inside the damage window, so the refusal below is about the next guest
        // and not about the clock.
        var now = CheckOut.AddHours(6);

        Assert.Equal(
            Shield.Refusal.NextGuestArrived,
            Shield.CanFile(Ask(ShieldCase.C1, now, contacted: CheckOut, nextGuest: now.AddHours(-1))).Reason);

        Assert.True(Shield.CanFile(Ask(ShieldCase.C1, now, contacted: CheckOut, nextGuest: now.AddDays(1))).Ok);
    }

    [Fact]
    public void A_host_case_with_nothing_claimed_is_refused()
    {
        Assert.Equal(
            Shield.Refusal.NothingClaimed,
            Shield.CanFile(Ask(ShieldCase.C1, CheckOut.AddDays(1), contacted: CheckOut, claimed: 0)).Reason);
    }

    [Fact]
    public void One_open_case_per_booking()
    {
        Assert.Equal(
            Shield.Refusal.AlreadyOpen,
            Shield.CanFile(Ask(ShieldCase.C1, CheckOut.AddDays(1), contacted: CheckOut, openCase: true)).Reason);
    }

    /* --------------------------------------------------- §3.1 C4 */

    [Fact]
    public void A_third_party_case_is_still_the_hosts_to_bring()
    {
        Assert.Equal(ShieldSide.Host, Shield.SideOf(ShieldCase.C4));
        Assert.True(Shield.IsThirdParty(ShieldCase.C4));
        Assert.False(Shield.IsThirdParty(ShieldCase.C1));
    }

    [Fact]
    public void A_third_party_case_needs_to_name_who_was_hurt()
    {
        Assert.True(Shield.CanFile(Ask(ShieldCase.C4, CheckOut.AddDays(2), contacted: CheckOut)).Ok);

        Assert.Equal(
            Shield.Refusal.NoThirdParty,
            Shield.CanFile(Ask(ShieldCase.C4, CheckOut.AddDays(2), contacted: CheckOut, thirdParty: null)).Reason);
    }

    [Fact]
    public void The_next_guest_arriving_does_not_bar_a_third_party_case()
    {
        var now = CheckOut.AddDays(3);

        // A neighbour's car is not inside the property, so the attribution
        // argument that closes C1 does not apply here.
        Assert.True(Shield.CanFile(
            Ask(ShieldCase.C4, now, contacted: CheckOut, nextGuest: now.AddHours(-1))).Ok);

        Assert.Equal(
            Shield.Refusal.NextGuestArrived,
            Shield.CanFile(Ask(ShieldCase.C1, now, contacted: CheckOut, nextGuest: now.AddHours(-1))).Reason);
    }

    [Fact]
    public void A_third_party_case_still_keeps_the_fortnight()
    {
        Assert.Equal(
            Shield.Refusal.WindowClosed,
            Shield.CanFile(Ask(ShieldCase.C4, CheckOut.AddDays(15), contacted: CheckOut)).Reason);
    }

    [Fact]
    public void The_host_carries_no_excess_for_somebody_elses_loss()
    {
        var own = Shield.SettleHost(2_000_000m, 0m, 0m, 0m);
        var neighbour = Shield.SettleHost(2_000_000m, 0m, 0m, 0m, thirdParty: true);

        Assert.Equal(500_000m, own.Deductible);
        Assert.Equal(1_500_000m, own.Approved);

        // The loss is not the host's, so charging them the first slice of it
        // would be charging them for somebody else's damage.
        Assert.Equal(0m, neighbour.Deductible);
        Assert.Equal(2_000_000m, neighbour.Approved);
    }

    [Fact]
    public void The_ceilings_still_bite_on_a_third_party_case()
    {
        var outcome = Shield.SettleHost(100_000_000m, 0m, 0m, 0m, thirdParty: true);

        Assert.Equal(75_000_000m, outcome.Approved);          // C-A, and no excess taken off
        Assert.Equal(25_000_000m, outcome.TrimmedByCeiling);
    }

    /* ------------------------------ §3.3, chốt 17/08/2026: sàn không gánh */

    /// <summary>
    /// The customer's rule: a guest who walks out without paying leaves the host
    /// carrying it. StayHost decides the number and pays none of it.
    /// </summary>
    [Fact]
    public void Damage_the_guest_refuses_to_pay_is_borne_by_the_host_not_the_fund()
    {
        var outcome = Shield.SettleHost(
            claimed: 3_000_000m, deposit: 0m, recoverableFromGuest: 0m, alreadyPaidThisYear: 0m,
            fundCovers: false);

        Assert.Equal(0m, outcome.FromFund);
        Assert.Equal(3_000_000m, outcome.BorneByHost);
        Assert.Equal(3_000_000m, outcome.Approved);
        Assert.Contains("không chi", outcome.Summary);
    }

    [Fact]
    public void Cash_handed_over_at_the_door_reduces_what_the_host_carries()
    {
        var outcome = Shield.SettleHost(
            claimed: 3_000_000m, deposit: 0m, recoverableFromGuest: 2_000_000m,
            alreadyPaidThisYear: 0m, fundCovers: false);

        Assert.Equal(2_000_000m, outcome.FromGuest);
        Assert.Equal(1_000_000m, outcome.BorneByHost);
        Assert.Equal(0m, outcome.FromFund);
    }

    [Fact]
    public void A_guest_who_pays_in_full_leaves_the_host_carrying_nothing()
    {
        var outcome = Shield.SettleHost(
            claimed: 3_000_000m, deposit: 0m, recoverableFromGuest: 3_000_000m,
            alreadyPaidThisYear: 0m, fundCovers: false);

        Assert.Equal(0m, outcome.BorneByHost);
        Assert.Equal(0m, outcome.FromFund);
        Assert.Contains("đã trả đủ", outcome.Summary);
    }

    /// <summary>
    /// The ceilings and the excess bound what the fund pays out. A ruling between
    /// two private people is not the fund paying out, so capping it at 75 million
    /// or docking the host's 500,000 excess would be inventing a rule nobody
    /// agreed to — and would understate what the guest actually owes.
    /// </summary>
    [Fact]
    public void A_ruling_the_fund_does_not_pay_is_not_capped_or_docked()
    {
        var outcome = Shield.SettleHost(
            claimed: 100_000_000m, deposit: 0m, recoverableFromGuest: 0m,
            alreadyPaidThisYear: 0m, fundCovers: false);

        Assert.Equal(100_000_000m, outcome.Approved);      // no C-A ceiling
        Assert.Equal(0m, outcome.Deductible);              // no C-C excess
        Assert.Equal(0m, outcome.TrimmedByCeiling);
        Assert.Equal(0m, outcome.FromFund);
    }

    /// <summary>Which kinds the fund still stands behind, and which it does not.</summary>
    [Fact]
    public void The_fund_covers_lost_income_and_a_neighbour_but_not_damage()
    {
        Assert.False(Shield.FundCovers(ShieldCase.C1));    // hư hỏng
        Assert.False(Shield.FundCovers(ShieldCase.C2));    // dọn dẹp, khử mùi
        Assert.True(Shield.FundCovers(ShieldCase.C3));     // mất thu nhập
        Assert.True(Shield.FundCovers(ShieldCase.C4));     // bên thứ ba
    }

    /// <summary>
    /// The fund's own cases are untouched by any of this: a neighbour cannot be
    /// paid at a door they were never at.
    /// </summary>
    [Fact]
    public void A_neighbours_claim_still_comes_out_of_the_fund()
    {
        var outcome = Shield.SettleHost(
            claimed: 2_000_000m, deposit: 0m, recoverableFromGuest: 0m, alreadyPaidThisYear: 0m,
            thirdParty: true, fundCovers: true);

        Assert.Equal(2_000_000m, outcome.FromFund);
        Assert.Equal(0m, outcome.Deductible);
    }

    [Fact]
    public void Nothing_can_be_filed_for_a_third_party_when_the_branch_is_off()
    {
        var previous = ShieldSettings.Current;
        try
        {
            ShieldSettings.Current = previous with { ThirdPartyBranch = false };

            Assert.Equal(
                Shield.Refusal.BranchOff,
                Shield.CanFile(Ask(ShieldCase.C4, CheckOut.AddDays(2), contacted: CheckOut)).Reason);

            // Everything else carries on regardless — inside C1's own window,
            // which is the checkout day rather than the fortnight.
            Assert.True(Shield.CanFile(Ask(ShieldCase.C1, CheckOut.AddHours(2), contacted: CheckOut)).Ok);
        }
        finally
        {
            ShieldSettings.Current = previous;
        }
    }

    [Fact]
    public void Money_for_a_third_party_never_lands_on_host_payables()
    {
        var claim = new ShieldClaim { Id = 1, Reference = "SS1", BookingId = 7, Kind = ShieldCase.C4 };

        var paid = Ledger.PayFromShield(claim, 0m, 0m, DateTime.UtcNow, toThirdParty: 3_000_000m);
        var charged = Ledger.ChargeForThirdParty(claim, 2_000_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(paid));
        Assert.Equal(0m, Ledger.Imbalance(charged));

        Assert.DoesNotContain(paid, e => e.Account == LedgerAccount.HostPayable);
        Assert.DoesNotContain(charged, e => e.Account == LedgerAccount.HostPayable);

        Assert.Equal(
            -5_000_000m,
            paid.Concat(charged).Where(e => e.Account == LedgerAccount.ThirdPartyPayable).Sum(e => e.Signed));
    }

    /* ------------------------------------------------------------- §2.3 */

    [Fact]
    public void A_host_cancellation_returns_the_whole_booking_and_adds_a_credit()
    {
        var outcome = Shield.SettleGuest(
            ShieldCase.K1, bookingTotal: 10_000_000m, hostPayout: 7_000_000m,
            nights: 3, nightsUnused: 3, expensesClaimed: 0m, rehousingDifference: 0m,
            ShieldRemedy.Refunded);

        Assert.Equal(10_000_000m, outcome.Refund);
        Assert.Equal(1_000_000m, outcome.Credit);              // K-B, 10%
        Assert.Equal(7_000_000m, outcome.FromHost);
        Assert.Equal(4_000_000m, outcome.FromFund);            // the rest, plus the credit
    }

    [Fact]
    public void A_partial_stay_returns_only_the_nights_the_guest_did_not_get()
    {
        var outcome = Shield.SettleGuest(
            ShieldCase.K3, 9_000_000m, 6_300_000m, nights: 3, nightsUnused: 2,
            0m, 0m, ShieldRemedy.Refunded);

        Assert.Equal(6_000_000m, outcome.Refund);
        Assert.Equal(0m, outcome.Credit);                      // only K1 carries a credit
        Assert.Equal(4_200_000m, outcome.FromHost);
    }

    [Fact]
    public void Expenses_are_capped_and_only_where_the_spec_allows_them()
    {
        var lockout = Shield.SettleGuest(
            ShieldCase.K2, 10_000_000m, 7_000_000m, 3, 3,
            expensesClaimed: 5_000_000m, rehousingDifference: 0m, remedy: ShieldRemedy.Refunded);

        var mismatch = Shield.SettleGuest(
            ShieldCase.K3, 10_000_000m, 7_000_000m, 3, 3,
            expensesClaimed: 5_000_000m, rehousingDifference: 0m, remedy: ShieldRemedy.Refunded);

        Assert.Equal(3_000_000m, lockout.Expenses);            // K-C ceiling
        Assert.Equal(0m, mismatch.Expenses);
    }

    [Fact]
    public void The_rehousing_top_up_stops_at_forty_percent_of_the_booking()
    {
        var outcome = Shield.SettleGuest(
            ShieldCase.K4, 10_000_000m, 7_000_000m, 3, 3,
            0m, rehousingDifference: 9_000_000m, remedy: ShieldRemedy.Rehoused);

        Assert.Equal(4_000_000m, outcome.RehousingTopUp);      // K-A, 40%
        Assert.Equal(0m, outcome.Refund);                      // the guest kept a stay
        Assert.Equal(4_000_000m, outcome.FromFund);
    }

    /* ------------------------------------------------------------- §3.2 */

    [Fact]
    public void The_host_carries_the_first_slice_themselves()
    {
        var outcome = Shield.SettleHost(
            claimed: 2_000_000m, deposit: 0m, recoverableFromGuest: 0m, alreadyPaidThisYear: 0m);

        Assert.Equal(500_000m, outcome.Deductible);            // C-C
        Assert.Equal(1_500_000m, outcome.Approved);
        Assert.Equal(1_500_000m, outcome.FromFund);
    }

    [Fact]
    public void The_per_claim_ceiling_bites_before_anything_else()
    {
        var outcome = Shield.SettleHost(100_000_000m, 0m, 0m, 0m);

        Assert.Equal(74_500_000m, outcome.Approved);           // C-A 75m, less the excess
        Assert.Equal(25_000_000m, outcome.TrimmedByCeiling);
    }

    [Fact]
    public void The_yearly_ceiling_counts_what_the_host_already_had()
    {
        var outcome = Shield.SettleHost(50_000_000m, 0m, 0m, alreadyPaidThisYear: 330_000_000m);

        // C-B leaves 20m of headroom, and the excess still comes off that.
        Assert.Equal(19_500_000m, outcome.Approved);
        Assert.Equal(30_000_000m, outcome.TrimmedByCeiling);
    }

    [Fact]
    public void Deposit_first_then_the_guest_then_the_fund_and_never_reordered()
    {
        var outcome = Shield.SettleHost(
            claimed: 10_500_000m, deposit: 2_000_000m, recoverableFromGuest: 3_000_000m,
            alreadyPaidThisYear: 0m);

        Assert.Equal(10_000_000m, outcome.Approved);           // less the 500k excess
        Assert.Equal(2_000_000m, outcome.FromDeposit);
        Assert.Equal(3_000_000m, outcome.FromGuest);
        Assert.Equal(5_000_000m, outcome.FromFund);

        // Everything approved is accounted for, from exactly three sources.
        Assert.Equal(outcome.Approved, outcome.FromDeposit + outcome.FromGuest + outcome.FromFund);
    }

    [Fact]
    public void A_deposit_that_covers_it_all_leaves_the_fund_alone()
    {
        var outcome = Shield.SettleHost(2_500_000m, deposit: 5_000_000m, recoverableFromGuest: 0m,
            alreadyPaidThisYear: 0m);

        Assert.Equal(2_000_000m, outcome.FromDeposit);
        Assert.Equal(0m, outcome.FromFund);
    }

    [Fact]
    public void An_expensive_item_only_counts_in_full_when_it_was_declared()
    {
        Assert.Equal(30_000_000m, Shield.AllowedForItem(30_000_000m, declared: true));
        Assert.Equal(15_000_000m, Shield.AllowedForItem(30_000_000m, declared: false));   // C-E
        Assert.Equal(4_000_000m, Shield.AllowedForItem(4_000_000m, declared: false));
    }

    [Fact]
    public void Lost_income_stops_at_five_nights()
    {
        Assert.Equal(6_000_000m, Shield.LostIncome(2_000_000m, 3));
        Assert.Equal(10_000_000m, Shield.LostIncome(2_000_000m, 9));   // C-D caps it at 5
    }

    /* --------------------------------------------------------------- §6 */

    [Fact]
    public void The_one_hour_promise_only_holds_with_a_desk_behind_it()
    {
        Assert.Equal(TimeSpan.FromHours(1), Shield.FirstResponseDue(ShieldCase.K2));
        Assert.Equal(TimeSpan.FromHours(4), Shield.FirstResponseDue(ShieldCase.K3));
        Assert.Equal(TimeSpan.FromHours(24), Shield.FirstResponseDue(ShieldCase.C1));

        var noDesk = new ShieldSettings { RoundTheClockDesk = false };
        Assert.Equal(TimeSpan.FromHours(4), Shield.FirstResponseDue(ShieldCase.K2, noDesk));
    }

    [Fact]
    public void Silence_for_a_day_sends_the_case_to_a_person()
    {
        var opened = new DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc);

        Assert.False(Shield.ResponseLapsed(opened, opened.AddHours(23)));
        Assert.True(Shield.ResponseLapsed(opened, opened.AddHours(24)));
    }

    [Fact]
    public void One_appeal_only_and_only_inside_a_week()
    {
        var decided = new DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc);

        Assert.True(Shield.CanAppeal(ShieldStatus.Settled, decided, false, decided.AddDays(6)));
        Assert.False(Shield.CanAppeal(ShieldStatus.Settled, decided, false, decided.AddDays(8)));
        Assert.False(Shield.CanAppeal(ShieldStatus.Settled, decided, true, decided.AddDays(1)));
        Assert.False(Shield.CanAppeal(ShieldStatus.Open, decided, false, decided.AddDays(1)));
    }

    /* --------------------------------------------------------------- §5 */

    [Fact]
    public void The_fund_takes_five_percent_of_the_service_fee()
    {
        Assert.Equal(5_000_000m, Shield.FundContribution(100_000_000m));   // Q-B
    }

    [Fact]
    public void Spending_four_fifths_of_the_month_raises_the_alarm()
    {
        Assert.False(Shield.FundAlarm(spentThisMonth: 3_900_000m, contributedThisMonth: 5_000_000m));
        Assert.True(Shield.FundAlarm(4_000_000m, 5_000_000m));             // Q-C, 80%
        Assert.True(Shield.FundAlarm(1m, 0m));                             // spending with nothing set aside
    }

    /* --------------------------------------------------------------- §7 */

    [Fact]
    public void A_fourth_case_in_a_year_goes_to_a_person()
    {
        Assert.False(Shield.NeedsManualReview(casesInLastYear: 3, flagged: false));
        Assert.True(Shield.NeedsManualReview(4, false));                   // A-A
        Assert.True(Shield.NeedsManualReview(0, flagged: true));
    }

    [Fact]
    public void An_empty_fund_sends_cases_to_a_person_rather_than_turning_them_away()
    {
        // docs/06 §5 — running out of money is the platform's problem, not the
        // user's, so the case still gets filed. It just cannot settle itself.
        Assert.True(Shield.NeedsManualReview(0, flagged: false, fundExhausted: true));
        Assert.False(Shield.NeedsManualReview(0, flagged: false, fundExhausted: false));
    }

    /* -------------------------------------------------------------- §11 */

    [Fact]
    public void Nothing_the_user_reads_may_sound_like_insurance()
    {
        Assert.True(Shield.ReadsAsInsurance("Bạn được bảo hiểm tới 75 triệu"));
        Assert.True(Shield.ReadsAsInsurance("quyền lợi bảo hiểm của chủ nhà"));

        // Accents missing is still the same word.
        Assert.True(Shield.ReadsAsInsurance("chuong trinh bao hiem"));

        Assert.False(Shield.ReadsAsInsurance("StayHost hỗ trợ bạn tới 75 triệu ₫"));
        Assert.False(Shield.ReadsAsInsurance("chính sách hỗ trợ của sàn"));
        Assert.False(Shield.ReadsAsInsurance(null));
    }

    [Fact]
    public void Every_case_and_status_has_wording_of_its_own()
    {
        foreach (var kind in Enum.GetValues<ShieldCase>())
        {
            var label = Shield.CaseLabel(kind);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.False(Shield.ReadsAsInsurance(label));
        }

        foreach (var status in Enum.GetValues<ShieldStatus>())
            Assert.False(string.IsNullOrWhiteSpace(Shield.StatusLabel(status)));
    }

    [Fact]
    public void Cases_land_on_the_side_they_belong_to()
    {
        Assert.Equal(ShieldSide.Guest, Shield.SideOf(ShieldCase.K1));
        Assert.Equal(ShieldSide.Guest, Shield.SideOf(ShieldCase.K4));
        Assert.Equal(ShieldSide.Host, Shield.SideOf(ShieldCase.C1));
        Assert.Equal(ShieldSide.Host, Shield.SideOf(ShieldCase.C3));
    }

    /* ------------------------------------------------------------ ledger */

    [Fact]
    public void Funding_paying_and_recovering_all_balance()
    {
        var claim = new ShieldClaim { Id = 1, Reference = "SS1", BookingId = 7 };

        var funded = Ledger.FundShield(5_000_000m, "09/2026", DateTime.UtcNow);
        var paid = Ledger.PayFromShield(claim, 2_000_000m, 1_000_000m, DateTime.UtcNow);
        var recovered = Ledger.RecoverToShield(claim, 1_000_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(funded));
        Assert.Equal(0m, Ledger.Imbalance(paid));
        Assert.Equal(0m, Ledger.Imbalance(recovered));

        // 5m in, 3m out, 1m back: the fund is left holding 3m.
        var fund = funded.Concat(paid).Concat(recovered)
            .Where(e => e.Account == LedgerAccount.ShieldFund)
            .Sum(e => e.Signed);
        Assert.Equal(-3_000_000m, fund);
    }

    [Fact]
    public void What_the_guest_is_made_to_pay_never_touches_the_fund()
    {
        var claim = new ShieldClaim { Id = 1, Reference = "SS1", BookingId = 7 };

        var charged = Ledger.ChargeCounterparty(claim, 2_000_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(charged));
        Assert.DoesNotContain(charged, e => e.Account == LedgerAccount.ShieldFund);
    }

    [Fact]
    public void Nothing_is_posted_for_a_case_worth_nothing()
    {
        var claim = new ShieldClaim { Id = 1, Reference = "SS1", BookingId = 7 };

        Assert.Empty(Ledger.FundShield(0m, "09/2026", DateTime.UtcNow));
        Assert.Empty(Ledger.PayFromShield(claim, 0m, 0m, DateTime.UtcNow));
        Assert.Empty(Ledger.RecoverToShield(claim, 0m, DateTime.UtcNow));
        Assert.Empty(Ledger.ChargeCounterparty(claim, 0m, DateTime.UtcNow));
    }
}
