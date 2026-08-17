using System.Globalization;
using System.Text;

namespace StayHost.Domain;

/// <summary>Where one transfer to a host has got to in the outside world.</summary>
public enum PayoutBatchStatus
{
    /// <summary>The platform has decided to pay it. Nothing has left any bank.</summary>
    Pending = 0,
    /// <summary>It is in a file somebody downloaded. Still nothing has left any bank.</summary>
    Exported = 1,
    /// <summary>The bank executed it. This is the only state that means money moved.</summary>
    Settled = 2,
    /// <summary>The bank refused it, or the operator found it wrong.</summary>
    Failed = 3
}

/// <summary>
/// docs/07 §12.3 and §13 — one transfer out of the platform's account into a
/// host's, covering however many bookings were due to that host that day.
///
/// It exists because option A of §13 says so in as many words: a licensed
/// gateway collects the guest's money into the platform's own account, and
/// "việc chia tiền cho chủ nhà sàn phải tự làm bằng chuyển khoản hàng loạt".
/// There is no API behind that sentence. A person downloads a file, uploads it
/// to internet banking, and comes back to say whether the bank took it.
///
/// Which is the whole reason this record exists rather than a boolean. Until it
/// reads <see cref="PayoutBatchStatus.Settled"/> the money is still the
/// platform's, and the ledger has to say so — writing "paid the host" when a CSV
/// is sitting in somebody's downloads folder is the same class of untruth as
/// confirming a stay nobody paid for.
/// </summary>
public class PayoutBatch
{
    public long Id { get; set; }

    /// <summary>Unique. What both the host's statement and this platform call it.</summary>
    public string Reference { get; set; } = "";

    public int HostId { get; set; }
    public HostProfile? Host { get; set; }

    /// <summary>What the bank is asked to move: gross payout less any debt recovered.</summary>
    public decimal Amount { get; set; }

    /// <summary>The part held back against <see cref="HostProfile.OwedToPlatform"/>.</summary>
    public decimal Deducted { get; set; }

    public int BookingCount { get; set; }

    /// <summary>
    /// Who to pay, copied at the moment the transfer was decided rather than read
    /// later. A host who changes bank the next morning must not silently move
    /// yesterday's transfer, and an operator reconciling a statement needs the
    /// details the file actually carried.
    /// </summary>
    public string BankName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";

    public PayoutBatchStatus Status { get; set; } = PayoutBatchStatus.Pending;

    public DateOnly DueOn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExportedAt { get; set; }
    public DateTime? SettledAt { get; set; }

    /// <summary>Which admin said the bank had executed it, and anything they added.</summary>
    public string? SettledBy { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// docs/07 §13 — turning a day's transfers into something internet banking will
/// accept, and reading back what the bank said.
/// </summary>
public static class PayoutFiles
{
    /// <summary>
    /// The columns. Vietnamese banks each publish their own bulk-transfer
    /// template and none of them agree, but every one of them is these six
    /// fields in some order — so the export is these six with a header row, and
    /// an operator maps them into their bank's sheet once.
    ///
    /// Deliberately not a bank-specific format: guessing one and being wrong
    /// produces a file that uploads and pays the wrong people.
    /// </summary>
    public static readonly string[] Columns =
        ["STT", "SoTaiKhoan", "TenNguoiHuong", "NganHang", "SoTien", "NoiDung"];

    /// <summary>What goes in the transfer's description field, and what comes back on the statement.</summary>
    public static string Memo(string reference) => $"StayHost {reference}";

    /// <summary>
    /// One line per transfer. UTF-8 with a BOM because the operator opens this in
    /// Excel, and Excel reads a BOM-less UTF-8 CSV as Windows-1252 — which turns
    /// every Vietnamese name in the file into mojibake, on the one screen where a
    /// name has to be right.
    /// </summary>
    public static string Csv(IEnumerable<PayoutBatch> batches)
    {
        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.AppendLine(string.Join(',', Columns));

        var n = 0;

        foreach (var batch in batches)
        {
            n++;
            sb.AppendLine(string.Join(',', [
                n.ToString(CultureInfo.InvariantCulture),
                Field(batch.AccountNumber),
                Field(batch.AccountName),
                Field(batch.BankName),
                // Đồng has no minor unit, and a bank template that meets "1500000.00"
                // either rejects the row or reads it as fifteen hundred.
                ((long)Math.Round(batch.Amount, MidpointRounding.AwayFromZero))
                    .ToString(CultureInfo.InvariantCulture),
                Field(Memo(batch.Reference))
            ]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// An account number is text, not a number: it can begin with a zero, and a
    /// spreadsheet that reads it as a number eats that zero and pays nobody.
    /// Quoting every field is the cheap way to keep it a string, and it also
    /// handles the commas inside Vietnamese names.
    /// </summary>
    private static string Field(string? value) =>
        '"' + (value ?? "").Replace("\"", "\"\"") + '"';

    /// <summary>The file's name, which is what an operator will be looking for a week later.</summary>
    public static string FileName(DateOnly day) => $"stayhost-chuyen-tien-{day:yyyy-MM-dd}.csv";

    /* ------------------------------------------------------------ the account */

    /// <summary>
    /// Whether there is enough to pay this host at all. Said as a reason rather
    /// than a boolean, because "cannot pay" with no explanation is what leaves
    /// money sitting for a month.
    /// </summary>
    public static string? Missing(string? bankName, string? accountName, string? accountNumber) =>
        string.IsNullOrWhiteSpace(accountNumber)
            ? "Chưa có số tài khoản nhận tiền. Chủ nhà cần khai lại trong phần Nhận tiền."
            : string.IsNullOrWhiteSpace(bankName)
                ? "Chưa có tên ngân hàng."
                : string.IsNullOrWhiteSpace(accountName)
                    ? "Chưa có tên chủ tài khoản."
                    : null;

    /// <summary>Only ever the last four reach a screen (docs/07 §14.3).</summary>
    public static string Mask(string? accountNumber)
    {
        var digits = new string((accountNumber ?? "").Where(char.IsDigit).ToArray());
        return digits.Length < 4 ? "••••" : "•••• " + digits[^4..];
    }

    /// <summary>
    /// docs/07 §12.5 — the bank refused it. The transfer goes back to the retry
    /// ladder rather than being written off, and the host is told in words that
    /// name what they can do about it.
    /// </summary>
    public static string RefusedNotice(string reference) =>
        $"Khoản chuyển {reference} bị ngân hàng từ chối. StayHost sẽ thử lại; " +
        "nếu tài khoản nhận tiền của bạn có gì thay đổi, hãy cập nhật lại giúp.";

    /// <summary>Said to a host the moment a transfer is lined up, which is not the moment it lands.</summary>
    public static string QueuedNotice(decimal amount, string what, string reference) =>
        $"{amount:#,##0}₫ cho {what} đã được lên lệnh chuyển (mã {reference}). " +
        "Tiền thường về tài khoản trong 1–2 ngày làm việc.";

    public static string SettledNotice(decimal amount, string what, string reference) =>
        $"{amount:#,##0}₫ cho {what} đã được chuyển đi (mã {reference}).";
}
