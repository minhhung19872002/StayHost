using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 TC-09 — promo campaigns and their codes, run from the admin console.
/// A coupon spends the platform's own money, so the desk that manages it is
/// Finance; every change is written to the audit log like any other money action.
/// </summary>
[ApiController]
[Route("api/admin/coupons")]
public class AdminCouponsController(StayHostDbContext db, AdminAudit audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CouponDto>>> List(CancellationToken ct)
    {
        if (await audit.RequireAsync(AdminScope.Finance, ct) is null)
            return StatusCode(403, new { message = "Cần quyền Tài chính để quản lý mã giảm giá." });

        // The redemption count is the number of live rows, never a column on the
        // coupon: the ledger is the source of truth (docs/00 §6.1).
        var used = await db.CouponRedemptions
            .Where(r => !r.Voided)
            .GroupBy(r => r.CouponId)
            .Select(g => new { CouponId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CouponId, x => x.Count, ct);

        var coupons = await db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

        return Ok(coupons.Select(c => ToDto(c, used.GetValueOrDefault(c.Id))).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CouponDto>> Create(
        [FromBody] SaveCouponRequest req, CancellationToken ct)
    {
        var admin = await audit.RequireAsync(AdminScope.Finance, ct);
        if (admin is null) return StatusCode(403, new { message = "Cần quyền Tài chính để tạo mã giảm giá." });

        var code = Coupons.Normalize(req.Code);
        if (code.Length < 3) return BadRequest(new { message = "Mã giảm giá cần ít nhất 3 ký tự." });
        if (!Enum.TryParse<CouponKind>(req.Kind, true, out var kind))
            return BadRequest(new { message = "Loại mã không hợp lệ." });
        if (req.Value <= 0) return BadRequest(new { message = "Giá trị giảm phải lớn hơn 0." });
        if (kind == CouponKind.Percentage && req.Value > 100)
            return BadRequest(new { message = "Giảm theo phần trăm không vượt quá 100%." });

        if (await db.Coupons.AnyAsync(c => c.Code == code, ct))
            return Conflict(new { message = $"Mã {code} đã tồn tại." });

        var coupon = new Coupon
        {
            Code = code,
            Campaign = (req.Campaign ?? "").Trim(),
            Kind = kind,
            Value = req.Value,
            MaxDiscount = req.MaxDiscount,
            MinBookingTotal = req.MinBookingTotal,
            StartsAt = req.StartsAt,
            EndsAt = req.EndsAt,
            MaxRedemptions = req.MaxRedemptions,
            MaxPerUser = req.MaxPerUser,
            IsActive = true
        };

        db.Coupons.Add(coupon);
        audit.Record(admin, "coupon.create", code, null,
            $"{kind} {req.Value}, giới hạn {req.MaxRedemptions?.ToString() ?? "∞"}");
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(coupon, 0));
    }

    /// <summary>
    /// docs/01 TC-09 — stopping a campaign. The coupon is deactivated rather than
    /// deleted, so the bookings that used it keep their record and the audit trail
    /// stays whole.
    /// </summary>
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var admin = await audit.RequireAsync(AdminScope.Finance, ct);
        if (admin is null) return StatusCode(403, new { message = "Cần quyền Tài chính." });

        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return NotFound();

        if (coupon.IsActive)
        {
            coupon.IsActive = false;
            audit.Record(admin, "coupon.deactivate", coupon.Code, "active", "inactive");
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    private static CouponDto ToDto(Coupon c, int used) => new(
        c.Id, c.Code, c.Campaign, c.Kind.ToString(), c.Value, c.MaxDiscount, c.MinBookingTotal,
        c.StartsAt, c.EndsAt, c.MaxRedemptions, c.MaxPerUser, used, c.IsActive, c.CreatedAt);
}
