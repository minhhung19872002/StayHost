using System.Globalization;
using System.Net;
using System.Text;
using StayHost.Domain;
using StayHost.Web.Contracts;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 ĐP-14 — renders a booking as a self-contained, printable invoice. All
/// styling is inline so the document stands alone when saved, and every value is
/// HTML-encoded because a guest name or listing title is untrusted text.
/// </summary>
public static class InvoiceHtml
{
    private static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    public static string Render(Booking booking, IReadOnlyList<PriceLineDto> lines)
    {
        var l = booking.Listing;
        var host = l?.Host;
        var paid = Invoices.AmountPaid(booking);

        var rows = new StringBuilder();
        foreach (var line in lines)
        {
            rows.Append("<tr><td>").Append(Enc(line.Label)).Append("</td><td class=\"num\">")
                .Append(Money(line.Amount)).Append("</td></tr>");
        }

        var balanceRow = Invoices.HasBalanceDue(booking)
            ? $"<tr class=\"muted\"><td>Đã thanh toán</td><td class=\"num\">{Money(paid)}</td></tr>" +
              $"<tr class=\"muted\"><td>Còn lại (thu ngày {booking.BalanceDueOn:dd/MM/yyyy})</td>" +
              $"<td class=\"num\">{Money(booking.BalanceDue)}</td></tr>"
            : "";

        return $$"""
        <!doctype html>
        <html lang="vi"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Hoá đơn {{Enc(Invoices.Number(booking))}}</title>
        <style>
          body { font-family: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
                 color: #222; max-width: 720px; margin: 32px auto; padding: 0 20px; }
          .head { display: flex; justify-content: space-between; align-items: flex-start;
                  border-bottom: 2px solid #ff385c; padding-bottom: 16px; }
          .brand { font-size: 22px; font-weight: 800; }
          .brand span { color: #ff385c; }
          h1 { font-size: 17px; margin: 24px 0 4px; }
          .meta { color: #666; font-size: 13px; line-height: 1.7; }
          table { width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 14px; }
          td { padding: 9px 0; border-bottom: 1px solid #eee; }
          .num { text-align: right; white-space: nowrap; }
          .muted td { color: #666; border-bottom: none; padding: 4px 0; }
          .total td { font-weight: 800; font-size: 16px; border-top: 2px solid #222; border-bottom: none; padding-top: 12px; }
          .note { color: #888; font-size: 12px; margin-top: 28px; line-height: 1.6; }
          @media print { body { margin: 0; } .noprint { display: none; } }
          .noprint { margin-top: 24px; }
          .btn { background: #ff385c; color: #fff; border: 0; padding: 10px 18px;
                 border-radius: 10px; font-size: 14px; cursor: pointer; }
        </style></head>
        <body>
          <div class="head">
            <div class="brand">StayHost<span> OS</span></div>
            <div class="meta"><b>Hoá đơn {{Enc(Invoices.Number(booking))}}</b><br>
              Ngày lập: {{booking.CreatedAt.ToString("dd/MM/yyyy", Vn)}}<br>
              Mã đơn: {{Enc(booking.Reference)}}</div>
          </div>

          <h1>Bên thuê</h1>
          <div class="meta">{{Enc(booking.GuestName ?? "Khách")}}{{Email(booking.GuestEmail)}}</div>

          <h1>Chỗ nghỉ</h1>
          <div class="meta">{{Enc(l?.Title ?? "")}}<br>
            {{Enc(JoinPlace(l?.City, l?.Country))}}{{HostLine(host?.Name)}}<br>
            Nhận phòng {{booking.CheckIn.ToString("dd/MM/yyyy", Vn)}} ·
            Trả phòng {{booking.CheckOut.ToString("dd/MM/yyyy", Vn)}} ·
            {{booking.Nights}} đêm · {{booking.Guests}} khách</div>

          <table>
            <tbody>
              {{rows}}
              <tr class="total"><td>Tổng cộng</td><td class="num">{{Money(booking.Total)}}</td></tr>
              {{balanceRow}}
            </tbody>
          </table>

          <p class="note">
            Giá đã gồm thuế và phí dịch vụ. Thuế do khách trả và StayHost nộp thay cơ quan thuế
            (docs/03 §1). Đây là hoá đơn xác nhận giao dịch trên nền tảng StayHost OS.
          </p>

          <div class="noprint"><button class="btn" onclick="window.print()">In hoặc lưu PDF</button></div>
        </body></html>
        """;
    }

    private static string Money(decimal v) => v.ToString("#,##0", Vn) + "₫";
    private static string Enc(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Email(string? e) => string.IsNullOrWhiteSpace(e) ? "" : "<br>" + Enc(e);
    private static string HostLine(string? h) => string.IsNullOrWhiteSpace(h) ? "" : "<br>Chủ nhà: " + Enc(h);

    private static string JoinPlace(string? city, string? country) =>
        string.Join(", ", new[] { city, country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
