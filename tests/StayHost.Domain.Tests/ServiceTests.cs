using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 MR-05 → MR-07 — services booked by the slot, at an address.</summary>
public class ServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static ServiceOffering Make(
        ServicePricing pricing = ServicePricing.PerSession, decimal price = 1_000_000m,
        bool travels = true, int radius = 10, bool partner = false, decimal commission = 0.15m) =>
        new()
        {
            Id = 1, Title = "Đầu bếp tại nhà", City = "Đà Nẵng", Country = "Việt Nam",
            Pricing = pricing, BasePrice = price, MinQuantity = 1, MaxQuantity = 8,
            DurationMinutes = 120, TravelsToGuest = travels, ServiceRadiusKm = radius,
            Latitude = 16.0544, Longitude = 108.2022, OpensAtHour = 8, ClosesAtHour = 20,
            IsPartner = partner, CommissionRate = commission
        };

    private static ServiceRules.Request Ask(
        ServiceOffering offering, int hoursAhead = 24, int quantity = 1,
        string? address = "12 Trần Phú, Đà Nẵng", double lat = 16.06, double lng = 108.21,
        params (DateTime From, DateTime To)[] busy) =>
        new()
        {
            Offering = offering,
            StartsAt = Now.AddHours(hoursAhead),
            Now = Now,
            Quantity = quantity,
            Address = address,
            Latitude = lat,
            Longitude = lng,
            Busy = busy
        };

    /* ------------------------------------------------------------- MR-06 */

    [Fact]
    public void An_ordinary_slot_can_be_booked()
    {
        Assert.True(ServiceRules.CanBook(Ask(Make())).Ok);
    }

    [Fact]
    public void Nothing_can_be_booked_for_two_hours_from_now()
    {
        Assert.Equal(
            ServiceRules.Refusal.TooSoon,
            ServiceRules.CanBook(Ask(Make(), hoursAhead: 2)).Reason);
    }

    [Fact]
    public void A_slot_outside_working_hours_is_refused()
    {
        // 24 hours on takes us back to 08:00; 37 lands at 21:00, past closing.
        Assert.Equal(
            ServiceRules.Refusal.OutsideHours,
            ServiceRules.CanBook(Ask(Make(), hoursAhead: 37)).Reason);
    }

    [Fact]
    public void A_job_that_would_run_past_closing_time_is_refused()
    {
        // Starts at 19:00 and runs two hours, which ends an hour after closing.
        Assert.Equal(
            ServiceRules.Refusal.OutsideHours,
            ServiceRules.CanBook(Ask(Make(), hoursAhead: 35)).Reason);
    }

    [Fact]
    public void Two_jobs_cannot_overlap_on_one_provider()
    {
        var start = Now.AddHours(24);

        Assert.Equal(
            ServiceRules.Refusal.AlreadyBooked,
            ServiceRules.CanBook(Ask(Make(), busy: (start.AddMinutes(60), start.AddMinutes(180)))).Reason);

        // Back to back is fine: the second starts as the first ends.
        Assert.True(ServiceRules.CanBook(Ask(Make(), busy: (start.AddMinutes(120), start.AddMinutes(240)))).Ok);
    }

    /* ------------------------------------------------------------- MR-05 */

    [Fact]
    public void An_address_beyond_the_service_radius_is_refused()
    {
        // Hội An is about 25 km from Đà Nẵng, well past a 10 km radius.
        var check = ServiceRules.CanBook(Ask(Make(radius: 10), lat: 15.8801, lng: 108.3380));

        Assert.Equal(ServiceRules.Refusal.OutOfRange, check.Reason);
        Assert.Contains("10 km", check.Message);
    }

    [Fact]
    public void The_same_address_is_fine_for_a_provider_who_travels_further()
    {
        Assert.True(ServiceRules.CanBook(Ask(Make(radius: 40), lat: 15.8801, lng: 108.3380)).Ok);
    }

    [Fact]
    public void A_provider_who_travels_needs_somewhere_to_go()
    {
        Assert.Equal(
            ServiceRules.Refusal.NoAddress,
            ServiceRules.CanBook(Ask(Make(), address: null)).Reason);
    }

    [Fact]
    public void A_provider_who_does_not_travel_needs_no_address_at_all()
    {
        Assert.True(ServiceRules.CanBook(Ask(Make(travels: false), address: null)).Ok);
    }

    [Fact]
    public void Distance_between_two_known_cities_is_about_right()
    {
        var km = ServiceRules.DistanceKm(16.0544, 108.2022, 15.8801, 108.3380);

        Assert.InRange(km, 20, 32);
    }

    [Fact]
    public void A_quantity_outside_what_the_provider_takes_is_refused()
    {
        Assert.Equal(
            ServiceRules.Refusal.QuantityOutOfRange,
            ServiceRules.CanBook(Ask(Make(), quantity: 20)).Reason);
    }

    /* ---------------------------------------------------------- pricing */

    [Fact]
    public void A_flat_rate_visit_is_one_price_however_many_are_named()
    {
        var one = Pricing.QuoteService(new Pricing.ServiceRequest
        { Offering = Make(), Quantity = 1, StartsAt = Now.AddDays(1) });
        var four = Pricing.QuoteService(new Pricing.ServiceRequest
        { Offering = Make(), Quantity = 4, StartsAt = Now.AddDays(1) });

        Assert.Equal(one.Subtotal, four.Subtotal);
    }

    [Fact]
    public void An_hourly_service_multiplies()
    {
        var price = Pricing.QuoteService(new Pricing.ServiceRequest
        {
            Offering = Make(ServicePricing.PerHour, 500_000m), Quantity = 3, StartsAt = Now.AddDays(1)
        });

        Assert.Equal(1_500_000m, price.Subtotal);
        Assert.Equal(0m, price.GuestServiceFee);        // docs/09 §3.3 — no guest service fee on services
        Assert.Equal(1_500_000m, price.Total);
    }

    [Fact]
    public void A_chef_massage_or_trainer_job_needs_its_mandatory_note()
    {
        // docs/09 §3.5 (scenario 10) — the safety note is required for these…
        Assert.True(ServiceRules.NoteMissing("chef", null));
        Assert.True(ServiceRules.NoteMissing("chef", "   "));
        Assert.True(ServiceRules.NoteMissing("massage", ""));
        Assert.True(ServiceRules.NoteMissing("fitness", null));
        Assert.False(ServiceRules.NoteMissing("chef", "Dị ứng hải sản"));

        // …and optional for everything else.
        Assert.False(ServiceRules.NoteMissing("photo", null));
        Assert.False(ServiceRules.NoteMissing("transfer", null));
        Assert.Equal(ServiceRules.NoteKind.FoodAllergy, ServiceRules.RequiredNote("chef"));
    }

    /* ------------------------------------------------------------- MR-07 */

    [Fact]
    public void A_partner_job_pays_commission_where_a_host_job_pays_the_host_fee()
    {
        var own = Pricing.QuoteService(new Pricing.ServiceRequest
        { Offering = Make(), Quantity = 1, StartsAt = Now.AddDays(1) });
        var partner = Pricing.QuoteService(new Pricing.ServiceRequest
        { Offering = Make(partner: true, commission: 0.18m), Quantity = 1, StartsAt = Now.AddDays(1) });

        Assert.Equal(150_000m, own.PlatformCut);       // docs/09 §3.3 — 15% provider fee (DV-F)
        Assert.Equal(180_000m, partner.PlatformCut);   // 18% commission

        // The guest pays the same either way; only the split behind it changes.
        Assert.Equal(own.Total, partner.Total);
        Assert.Equal(850_000m, own.ProviderPayout);
        Assert.Equal(820_000m, partner.ProviderPayout);
    }

    [Fact]
    public void The_lines_shown_add_up_to_the_total()
    {
        var price = Pricing.QuoteService(new Pricing.ServiceRequest
        {
            Offering = Make(ServicePricing.PerPerson, 400_000m), Quantity = 3, StartsAt = Now.AddDays(1),
            TaxRules = [new TaxRule
            {
                Id = 1, Country = "Việt Nam", Name = "VAT",
                Method = TaxMethod.Percentage, Value = 0.08m, Base = TaxBase.Subtotal
            }]
        });

        Assert.Equal(price.Total, price.Lines.Sum(l => l.Amount));
    }

    /* --------------------------------------------------------- refunds */

    [Fact]
    public void Capturing_and_refunding_a_job_leaves_every_account_flat()
    {
        var price = Pricing.QuoteService(new Pricing.ServiceRequest
        {
            Offering = Make(partner: true), Quantity = 1, StartsAt = Now.AddDays(1),
            TaxRules = [new TaxRule
            {
                Id = 1, Country = "Việt Nam", Name = "VAT",
                Method = TaxMethod.Percentage, Value = 0.08m, Base = TaxBase.Subtotal
            }]
        });

        var booking = new ServiceBooking
        {
            Id = 1, Reference = "SV1",
            Subtotal = price.Subtotal, ServiceFee = price.GuestServiceFee, Tax = price.Tax,
            Total = price.Total, PlatformCut = price.PlatformCut, ProviderPayout = price.ProviderPayout
        };

        var captured = Ledger.CaptureService(booking, price, Now);
        var refunded = Ledger.RefundService(booking, booking.Total, Now);

        Assert.Equal(0m, Ledger.Imbalance(captured));
        Assert.Equal(0m, Ledger.Imbalance(refunded));

        foreach (var account in Enum.GetValues<LedgerAccount>())
            Assert.Equal(0m, captured.Concat(refunded).Where(e => e.Account == account).Sum(e => e.Signed));
    }

    [Fact]
    public void A_guest_gets_everything_back_up_to_a_day_before()
    {
        var booking = new ServiceBooking { Total = 1_140_000m, StartsAt = Now.AddHours(25) };
        Assert.Equal(1_140_000m, ServiceRules.GuestRefund(booking, Now));

        booking.StartsAt = Now.AddHours(23);
        Assert.Equal(0m, ServiceRules.GuestRefund(booking, Now));
    }
}
