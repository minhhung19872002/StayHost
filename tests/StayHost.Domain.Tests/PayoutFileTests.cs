using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §13 and §14.3 — keeping a host's account number safe, and getting it
/// into a file a bank will act on.
///
/// Both halves are the sort of thing that fails quietly. A cipher that cannot be
/// opened leaves every host unpayable with no error anywhere; a CSV that drops a
/// leading zero pays a stranger.
/// </summary>
public class PayoutFileTests
{
    private static readonly byte[] Key = Convert.FromBase64String(SecretText.NewKey());

    /* --------------------------------------------------------- §14.3, the key */

    [Fact]
    public void An_account_number_survives_a_round_trip()
    {
        var sealedText = SecretText.Seal("0123456789", Key);

        Assert.DoesNotContain("0123456789", sealedText);
        Assert.Equal("0123456789", SecretText.Open(sealedText, Key));
    }

    /// <summary>
    /// The same number twice must not produce the same ciphertext, or a database
    /// dump tells you which hosts bank in the same place.
    /// </summary>
    [Fact]
    public void The_same_number_seals_differently_every_time()
    {
        Assert.NotEqual(SecretText.Seal("0123456789", Key), SecretText.Seal("0123456789", Key));
    }

    /// <summary>
    /// AES-GCM, so an edited row fails to open rather than opening as something
    /// else. The difference matters: the second kind would transfer money to a
    /// number nobody chose.
    /// </summary>
    [Fact]
    public void A_tampered_value_does_not_open()
    {
        var sealedText = SecretText.Seal("0123456789", Key);
        var bytes = Convert.FromBase64String(sealedText);
        bytes[^1] ^= 0xFF;

        Assert.Null(SecretText.Open(Convert.ToBase64String(bytes), Key));
    }

    [Fact]
    public void The_wrong_key_does_not_open_it_and_does_not_throw()
    {
        var sealedText = SecretText.Seal("0123456789", Key);
        var other = Convert.FromBase64String(SecretText.NewKey());

        Assert.Null(SecretText.Open(sealedText, other));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 at all")]
    [InlineData("YWJj")]                       // valid base64, far too short to be a payload
    public void Nonsense_opens_to_nothing_rather_than_throwing(string? stored)
    {
        Assert.Null(SecretText.Open(stored, Key));
    }

    /// <summary>
    /// A short key silently stretched is the kind of thing that looks like
    /// encryption for years, so it is refused instead.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("bm90IGxvbmcgZW5vdWdo")]
    public void A_key_that_is_not_the_right_length_is_refused(string configured)
    {
        Assert.Null(SecretText.ReadKey(configured));
    }

    [Fact]
    public void A_key_may_be_written_in_base64_or_hex()
    {
        var bytes = Convert.FromBase64String(SecretText.NewKey());

        Assert.Equal(bytes, SecretText.ReadKey(Convert.ToBase64String(bytes)));
        Assert.Equal(bytes, SecretText.ReadKey(Convert.ToHexString(bytes)));
    }

    /* ---------------------------------------------------------- §13, the file */

    private static PayoutBatch Batch(string account, string name, decimal amount) => new()
    {
        Reference = "CT-260817-1",
        AccountNumber = account,
        AccountName = name,
        BankName = "MB Bank",
        Amount = amount
    };

    /// <summary>
    /// The failure that costs money: a spreadsheet reads 0123456789 as a number,
    /// eats the leading zero, and the transfer goes to 123456789 — an account
    /// that exists and belongs to somebody else.
    /// </summary>
    [Fact]
    public void An_account_number_keeps_its_leading_zero()
    {
        var csv = PayoutFiles.Csv([Batch("0123456789", "NGUYEN VAN A", 1_500_000m)]);

        Assert.Contains("\"0123456789\"", csv);
    }

    [Fact]
    public void The_amount_is_whole_dong_with_no_decimal_point()
    {
        var csv = PayoutFiles.Csv([Batch("0123456789", "NGUYEN VAN A", 1_500_000.4m)]);

        Assert.Contains(",1500000,", csv);
        Assert.DoesNotContain("1500000.", csv);
    }

    /// <summary>Excel reads a BOM-less UTF-8 CSV as Windows-1252 and mangles every name in it.</summary>
    [Fact]
    public void The_file_starts_with_a_byte_order_mark()
    {
        Assert.StartsWith("﻿", PayoutFiles.Csv([Batch("0123456789", "NGUYỄN VĂN A", 10_000m)]));
    }

    [Fact]
    public void A_comma_in_a_name_does_not_become_a_column()
    {
        var csv = PayoutFiles.Csv([Batch("0123456789", "NGUYEN VAN A, JR", 10_000m)]);
        var row = csv.Split('\n')[1];

        Assert.Contains("\"NGUYEN VAN A, JR\"", row);

        // Six columns still — quoting is what keeps the comma inside its field.
        // A reader that ignores the quotes finds seven, which is exactly the bug.
        Assert.Equal(PayoutFiles.Columns.Length, Fields(row).Count);
        Assert.Equal("NGUYEN VAN A, JR", Fields(row)[2]);
    }

    /// <summary>The smallest CSV reader that respects quotes, so the test does not
    /// prove the writer agrees with itself.</summary>
    private static List<string> Fields(string row)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < row.Length; i++)
        {
            var c = row[i];

            if (quoted && c == '"' && i + 1 < row.Length && row[i + 1] == '"') { current.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }

        fields.Add(current.ToString().TrimEnd('\r'));
        return fields;
    }

    [Fact]
    public void Every_row_is_numbered_and_carries_its_reference()
    {
        var csv = PayoutFiles.Csv([
            Batch("0123456789", "A", 1_000m),
            Batch("9876543210", "B", 2_000m)
        ]);

        var lines = csv.TrimEnd().Split('\n');

        Assert.Equal(3, lines.Length);                       // header + two rows
        Assert.StartsWith("1,", lines[1]);
        Assert.StartsWith("2,", lines[2]);
        Assert.Contains("StayHost CT-260817-1", lines[1]);
    }

    /* --------------------------------------------------- what stops a transfer */

    [Fact]
    public void A_host_with_no_account_number_cannot_be_paid_and_is_told_why()
    {
        Assert.NotNull(PayoutFiles.Missing("MB Bank", "NGUYEN VAN A", null));
        Assert.NotNull(PayoutFiles.Missing("MB Bank", "NGUYEN VAN A", "  "));
        Assert.NotNull(PayoutFiles.Missing(null, "NGUYEN VAN A", "0123456789"));
        Assert.NotNull(PayoutFiles.Missing("MB Bank", null, "0123456789"));
        Assert.Null(PayoutFiles.Missing("MB Bank", "NGUYEN VAN A", "0123456789"));
    }

    /// <summary>docs/07 §14.3 — only the last four ever reach a screen.</summary>
    [Fact]
    public void Only_the_last_four_digits_are_shown()
    {
        Assert.Equal("•••• 6789", PayoutFiles.Mask("0123456789"));
        Assert.Equal("••••", PayoutFiles.Mask(null));
        Assert.DoesNotContain("012345", PayoutFiles.Mask("0123456789"));
    }

    /// <summary>
    /// A transfer that has only been written down is not a transfer that
    /// happened, and the wording a host reads has to keep those apart.
    /// </summary>
    [Fact]
    public void A_queued_transfer_does_not_claim_the_money_has_moved()
    {
        var queued = PayoutFiles.QueuedNotice(1_500_000m, "2 đơn", "CT-260817-1");
        var settled = PayoutFiles.SettledNotice(1_500_000m, "2 đơn", "CT-260817-1");

        Assert.Contains("lên lệnh", queued);
        Assert.DoesNotContain("đã được chuyển đi", queued);
        Assert.Contains("đã được chuyển đi", settled);
    }
}
